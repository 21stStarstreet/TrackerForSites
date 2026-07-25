using System.Threading.Channels;
using TrackerForSites.Api.Data;
using TrackerForSites.Api.Models.Dtos;
using TrackerForSites.Api.Models.Entities;

namespace TrackerForSites.Api.Services;

/// <summary>
/// Collect endpoint'inden gelen ham event'leri bellekte bir kuyruğa alır,
/// arka planda işler ve toplu (batch) olarak veritabanına yazar.
///
/// NEDEN:
///   CollectController şu an her istekte:
///     - ip-api.com'a GeoIP isteği atar (~50-200ms)
///     - PostgreSQL'e INSERT yapar (~10ms)
///   → Yanıt süresi ~70-220ms. Yüksek trafikte DB bağlantıları tükenir.
///
///   Bu servis ile:
///     - CollectController yalnızca api_key doğrular (~10ms) ve kuyruğa atar (mikrosaniye)
///     - Hemen 204 döner (~12ms toplam)
///     - GeoIP + fingerprint + UA parse + DB write arka planda yapılır
///     - 50'li gruplar halinde INSERT → tek INSERT'ten çok daha verimli
///
/// SINIR:
///   Kuyruk bellekte (in-memory). Sunucu yeniden başlarsa işlenmemiş
///   event'ler kaybolur. Analitik için kabul edilebilir.
/// </summary>
public interface IEventQueue
{
    /// <summary>Event'i kuyruğa ekle. Kuyruk doluysa false döner (en eski düşürülür).</summary>
    bool TryEnqueue(EventQueueItem item);
}

/// <summary>
/// Kuyruk öğesi: CollectController'dan gelen ham veriler.
/// İşleme (fingerprint, geoip, UA parse) arka planda yapılır.
/// </summary>
public record EventQueueItem(
    Guid SiteId,
    string IpAddress,
    string UserAgent,
    CollectRequest Request
);

public class EventQueueService : BackgroundService, IEventQueue
{
    // Sınırlı kanal: 10.000 event sığar.
    // Dolu olduğunda en eski düşürülür (DropOldest) — analitik için kabul edilebilir.
    private readonly Channel<EventQueueItem> _channel;
    private readonly IServiceScopeFactory _scopeFactory;

    // Singleton servisler doğrudan inject edilebilir
    private readonly FingerprintService _fingerprint;
    private readonly UserAgentService _ua;
    private readonly GeoIpService _geoIp;
    private readonly ILogger<EventQueueService> _logger;

    // Batch boyutu: 50 event → tek AddRange + SaveChanges
    private const int BatchSize = 50;

    public EventQueueService(
        IServiceScopeFactory scopeFactory,
        FingerprintService fingerprint,
        UserAgentService ua,
        GeoIpService geoIp,
        ILogger<EventQueueService> logger)
    {
        _channel = Channel.CreateBounded<EventQueueItem>(new BoundedChannelOptions(10_000)
        {
            FullMode     = BoundedChannelFullMode.DropOldest,
            SingleReader = true, // Yalnızca bu servis okur → lock gereksiz
        });
        _scopeFactory = scopeFactory;
        _fingerprint  = fingerprint;
        _ua           = ua;
        _geoIp        = geoIp;
        _logger       = logger;
    }

    /// <inheritdoc />
    public bool TryEnqueue(EventQueueItem item) =>
        _channel.Writer.TryWrite(item);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventQueueService başladı.");
        var batch = new List<Event>(BatchSize);

        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    var ev = await BuildEventAsync(item);
                    batch.Add(ev);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Event işlenemedi: {SiteId}", item.SiteId);
                }

                // Flush: batch dolduğunda VEYA kanal geçici boşaldığında
                bool shouldFlush = batch.Count >= BatchSize
                                || (batch.Count > 0 && _channel.Reader.Count == 0);

                if (shouldFlush)
                {
                    await FlushAsync(batch, stoppingToken);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Uygulama kapatılıyor — normal durum
        }

        // Kapanışta kalan event'leri kaydet (max 10 sn)
        if (batch.Count > 0)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await FlushAsync(batch, cts.Token);
        }

        _logger.LogInformation("EventQueueService durdu.");
    }

    /// <summary>Ham kuyruk öğesini işlenmiş Event entity'sine dönüştürür.</summary>
    private async Task<Event> BuildEventAsync(EventQueueItem item)
    {
        var req = item.Request;

        var fingerprintHash = _fingerprint.Generate(item.IpAddress, item.UserAgent, req.L, req.W);
        var ipHash          = _fingerprint.HashIp(item.IpAddress);
        // Ham IP artık kullanılmıyor, GC toplayacak: GDPR uyumu
        var parsed          = _ua.Parse(item.UserAgent);
        var geo             = await _geoIp.LookupAsync(item.IpAddress); // Hata → null

        string? referrerDomain = null;
        if (!string.IsNullOrWhiteSpace(req.R) &&
            Uri.TryCreate(req.R, UriKind.Absolute, out var refUri))
            referrerDomain = refUri.Host.Replace("www.", "");

        DateTimeOffset? clientTs = req.Ts.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(req.Ts.Value)
            : null;

        return new Event
        {
            SiteId         = item.SiteId,
            EventType      = "pageview",
            Url            = req.U!, // CollectController'da null kontrolü yapıldı
            Referrer       = req.R,
            ReferrerDomain = referrerDomain,
            PageTitle      = req.Ti,
            Language       = req.L != null ? req.L[..Math.Min(req.L.Length, 10)] : null,
            ScreenWidth    = req.W,
            SessionId      = req.Id ?? Guid.NewGuid().ToString(),
            ClientTs       = clientTs,
            IpHash         = ipHash,
            UserAgent      = item.UserAgent,
            Fingerprint    = fingerprintHash,
            Browser        = parsed.Browser,
            Os             = parsed.Os,
            DeviceType     = parsed.DeviceType,
            CountryCode    = geo?.CountryCode,
            City           = geo?.City,
            ServerTs       = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Batch'i veritabanına yazar. Başarısız olursa logla, uygulama çökme.</summary>
    private async Task FlushAsync(List<Event> batch, CancellationToken ct)
    {
        try
        {
            // Her flush için yeni scoped DB context (singleton servisden scoped inject edilemez)
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Events.AddRange(batch);
            await db.SaveChangesAsync(ct);
            _logger.LogDebug("Veritabanına {Count} event yazıldı.", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch flush başarısız. {Count} event kaybedildi.", batch.Count);
        }
    }
}
