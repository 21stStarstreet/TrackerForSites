using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackerForSites.Api.Data;
using TrackerForSites.Api.Models.Dtos;
using TrackerForSites.Api.Models.Entities;
using TrackerForSites.Api.Services;

namespace TrackerForSites.Api.Controllers;

/// <summary>
/// tracker.js'ten gelen event'leri alır ve DB'ye yazar.
///
/// ENDPOINT: POST /api/collect
///
/// AKIŞ:
/// 1. Body'den CollectRequest al
/// 2. api_key ile siteyi doğrula
/// 3. Bot filtresi uygula (sunucu tarafı)
/// 4. IP al -> fingerprint üret -> IP hash'le
/// 5. UA parse et -> browser/os/device
/// 6. GeoIP sorgula -> country/city
/// 7. Event'i DB'ye yaz
/// 8. 204 No Content döndür (body yok, hızlı)
/// </summary>
[ApiController]
[Route("api")]
public class CollectController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly FingerprintService _fingerprint;
    private readonly UserAgentService _ua;
    private readonly GeoIpService _geoIp;
    private readonly ILogger<CollectController> _logger;

    // Sunucu tarafı bot filtresi, tracker.js'i bypass eden botlar için
    private static readonly System.Text.RegularExpressions.Regex BotPattern =
        new(@"bot|crawl|spider|headless|puppet|phantom|selenium|slurp|mediapartners",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public CollectController(
        AppDbContext db,
        FingerprintService fingerprint,
        UserAgentService ua,
        GeoIpService geoIp,
        ILogger<CollectController> logger)
    {
        _db          = db;
        _fingerprint = fingerprint;
        _ua          = ua;
        _geoIp       = geoIp;
        _logger      = logger;
    }

    [HttpPost("collect")]
    public async Task<IActionResult> Collect([FromBody] CollectRequest req)
    {
        // 1. Temel validasyon
        if (string.IsNullOrWhiteSpace(req.S) || string.IsNullOrWhiteSpace(req.U))
            return BadRequest();

        // 2. Site doğrulama, api_key geçerli mi?
        var site = await _db.Sites
            .Where(s => s.ApiKey == req.S && s.IsActive)
            .Select(s => new { s.Id })   // Sadece Id al, tüm nesneyi yükleme
            .FirstOrDefaultAsync();

        if (site is null)
            return Unauthorized(); // Geçersiz api_key

        // 3. Sunucu tarafı bot filtresi
        var userAgent = Request.Headers.UserAgent.ToString();
        if (BotPattern.IsMatch(userAgent))
            return NoContent(); // Botu sessizce kabul et, kaydetme

        // 4. IP adresini al
        // X-Forwarded-For: proxy/load balancer arkasındaki gerçek IP
        var ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()
                        ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown";

        // Birden fazla proxy varsa ilk IP gerçek istemcidir
        // "1.2.3.4, 5.6.7.8" -> "1.2.3.4"
        if (ipAddress.Contains(','))
            ipAddress = ipAddress.Split(',')[0].Trim();

        // 5. Fingerprint üret ve IP'yi hash'le
        var fingerprintHash = _fingerprint.Generate(ipAddress, userAgent, req.L, req.W);
        var ipHash          = _fingerprint.HashIp(ipAddress);
        // Ham IP artık kullanılmıyor, GC toplayacak: GDPR uyumu

        // 6. UA parse et
        var parsed = _ua.Parse(userAgent);

        // 7. GeoIP (async, hata olursa null döner, event yine kaydedilir)
        var geo = await _geoIp.LookupAsync(ipAddress);

        // 8. Referrer domain'ini çıkar
        string? referrerDomain = null;
        if (!string.IsNullOrWhiteSpace(req.R) && Uri.TryCreate(req.R, UriKind.Absolute, out var refUri))
            referrerDomain = refUri.Host.Replace("www.", "");

        // 9. client_ts dönüştür: Unix ms -> DateTimeOffset
        DateTimeOffset? clientTs = req.Ts.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(req.Ts.Value)
            : null;

        // 10. Event oluştur ve kaydet
        var ev = new Event
        {
            SiteId        = site.Id,
            EventType     = "pageview",
            Url           = req.U,
            Referrer      = req.R,
            ReferrerDomain = referrerDomain,
            PageTitle     = req.Ti,
            Language      = req.L?[..Math.Min(req.L.Length, 10)], // max 10 karakter
            ScreenWidth   = req.W,
            SessionId     = req.Id ?? Guid.NewGuid().ToString(),
            ClientTs      = clientTs,
            IpHash        = ipHash,
            UserAgent     = userAgent,
            Fingerprint   = fingerprintHash,
            Browser       = parsed.Browser,
            Os            = parsed.Os,
            DeviceType    = parsed.DeviceType,
            CountryCode   = geo?.CountryCode,
            City          = geo?.City,
            ServerTs      = DateTimeOffset.UtcNow
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        _logger.LogDebug("Event saved: {Site} | {Url} | {Device}",
            req.S, req.U, parsed.DeviceType);

        // 204 No Content: body yok, en hızlı response
        // tracker.js sendBeacon zaten response'u okumaz
        return NoContent();
    }
}
