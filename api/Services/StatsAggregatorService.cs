using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TrackerForSites.Api.Data;
using TrackerForSites.Api.Models.Entities;

namespace TrackerForSites.Api.Services;

/// <summary>
/// Gece çalışan istatistik toplayıcı.
///
/// NEDEN AYRI BİR SERVIS?
/// Her dashboard sorgusu milyonlarca event satırını toplamak zorunda kalmamalı.
/// Bunun yerine günlük özetleri önceden hesaplayıp daily_stats'a yazıyoruz.
/// Dashboard okurken sadece daily_stats'a bakıyor → çok hızlı.
///
/// ÇALIŞMA ZAMANI:
/// Her gece 00:05 UTC → bir önceki günün verisini işler.
/// (00:00 değil çünkü gece yarısı gelen son event'lerin yazılması için
/// birkaç dakika bekliyoruz.)
///
/// UPSERT STRATEJİSİ:
/// Aynı gün için kayıt varsa güncelle, yoksa ekle.
/// Bu sayede servis birden fazla çalışsa bile veri bozulmaz (idempotent).
/// </summary>
public class StatsAggregatorService : BackgroundService
{
    // BackgroundService: .NET'in yerleşik arka plan servis altyapısı.
    // ExecuteAsync uygulama başladığında çalışmaya başlar,
    // uygulama kapanana kadar devam eder.

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StatsAggregatorService> _logger;

    public StatsAggregatorService(
        IServiceScopeFactory scopeFactory,
        ILogger<StatsAggregatorService> logger)
    {
        // BackgroundService singleton'dır ama AppDbContext scoped.
        // Doğrudan inject edemeyiz — her çalışmada yeni scope açarız.
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StatsAggregator başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Bir sonraki çalışmaya kadar bekle
            var delay = GetDelayUntilNextRun();
            _logger.LogInformation("Sonraki istatistik hesabı: {Next:HH:mm} UTC",
                DateTime.UtcNow.Add(delay));

            await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            await RunAggregationAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Bir önceki günün tüm siteleri için istatistikleri hesaplar.
    /// </summary>
    private async Task RunAggregationAsync(CancellationToken ct)
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        _logger.LogInformation("İstatistik hesaplanıyor: {Date}", yesterday);

        try
        {
            // Her çalışmada yeni bir DB scope açıyoruz
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Aktif tüm siteleri al
            var siteIds = await db.Sites
                .Where(s => s.IsActive)
                .Select(s => s.Id)
                .ToListAsync(ct);

            foreach (var siteId in siteIds)
            {
                await AggregateForSiteAsync(db, siteId, yesterday, ct);
            }

            _logger.LogInformation("İstatistik hesabı tamamlandı: {Count} site", siteIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İstatistik hesaplamasında hata oluştu.");
        }
    }

    /// <summary>
    /// Tek bir site + tek bir gün için daily_stats hesaplar ve UPSERT eder.
    /// </summary>
    private static async Task AggregateForSiteAsync(
        AppDbContext db, Guid siteId, DateOnly date, CancellationToken ct)
    {
        // Günün başlangıcı ve sonu (UTC)
        var dayStart = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);
        var dayEnd   = dayStart.AddDays(1);

        // O gün o siteye ait tüm event'leri çek
        var events = await db.Events
            .Where(e => e.SiteId == siteId
                     && e.ServerTs >= dayStart
                     && e.ServerTs <  dayEnd)
            .Select(e => new
            {
                e.SessionId,
                e.Fingerprint,
                e.Url,
                e.ReferrerDomain,
                e.CountryCode,
                e.Browser,
                e.DeviceType
            })
            .ToListAsync(ct);

        if (events.Count == 0) return; // O gün event yoksa atla

        // ── Temel metrikler ───────────────────────────────────────────
        var pageviews       = events.Count;
        var uniqueVisitors  = events.Select(e => e.Fingerprint).Distinct().Count();
        var uniqueSessions  = events.Select(e => e.SessionId).Distinct().Count();

        // Bounce rate: sadece 1 sayfa görüntüleyen session'ların oranı
        var sessionPageCounts = events
            .GroupBy(e => e.SessionId)
            .Select(g => g.Count())
            .ToList();
        var bouncedSessions = sessionPageCounts.Count(c => c == 1);
        var bounceRate      = uniqueSessions > 0
            ? Math.Round((decimal)bouncedSessions / uniqueSessions, 3)
            : 0m;

        // ── JSONB alanları ────────────────────────────────────────────
        var topPages = events
            .GroupBy(e => e.Url)
            .Select(g => new { url = g.Key, views = g.Count() })
            .OrderByDescending(x => x.views)
            .Take(10)
            .ToList();

        var topReferrers = events
            .Where(e => e.ReferrerDomain != null)
            .GroupBy(e => e.ReferrerDomain)
            .Select(g => new { domain = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToList();

        var countryBreakdown = events
            .Where(e => e.CountryCode != null)
            .GroupBy(e => e.CountryCode)
            .ToDictionary(g => g.Key!, g => g.Count());

        var browserBreakdown = events
            .Where(e => e.Browser != null)
            .GroupBy(e => e.Browser)
            .ToDictionary(g => g.Key!, g => g.Count());

        var deviceBreakdown = events
            .GroupBy(e => e.DeviceType ?? "unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        // ── UPSERT ───────────────────────────────────────────────────
        // Varsa güncelle, yoksa ekle.
        // EF Core'da native UPSERT: ExecuteSqlRaw veya AddOrUpdate.
        // Biz mevcut kaydı çekip güncelliyoruz — basit ve güvenli.
        var existing = await db.DailyStats
            .Where(d => d.SiteId == siteId && d.StatDate == date)
            .FirstOrDefaultAsync(ct);

        if (existing is null)
        {
            db.DailyStats.Add(new DailyStat
            {
                SiteId           = siteId,
                StatDate         = date,
                Pageviews        = pageviews,
                UniqueVisitors   = uniqueVisitors,
                UniqueSessions   = uniqueSessions,
                BounceRate       = bounceRate,
                TopPages         = JsonSerializer.Serialize(topPages),
                TopReferrers     = JsonSerializer.Serialize(topReferrers),
                CountryBreakdown = JsonSerializer.Serialize(countryBreakdown),
                BrowserBreakdown = JsonSerializer.Serialize(browserBreakdown),
                DeviceBreakdown  = JsonSerializer.Serialize(deviceBreakdown),
                UpdatedAt        = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Pageviews        = pageviews;
            existing.UniqueVisitors   = uniqueVisitors;
            existing.UniqueSessions   = uniqueSessions;
            existing.BounceRate       = bounceRate;
            existing.TopPages         = JsonSerializer.Serialize(topPages);
            existing.TopReferrers     = JsonSerializer.Serialize(topReferrers);
            existing.CountryBreakdown = JsonSerializer.Serialize(countryBreakdown);
            existing.BrowserBreakdown = JsonSerializer.Serialize(browserBreakdown);
            existing.DeviceBreakdown  = JsonSerializer.Serialize(deviceBreakdown);
            existing.UpdatedAt        = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Bir sonraki 00:05 UTC'ye kadar beklenecek süreyi hesaplar.
    /// </summary>
    private static TimeSpan GetDelayUntilNextRun()
    {
        var now    = DateTime.UtcNow;
        var target = now.Date.AddDays(1).AddMinutes(5); // yarın 00:05 UTC
        var delay  = target - now;

        // Eğer delay 0'dan küçükse (zaten geçmişse) bir sonraki güne ayarla
        if (delay <= TimeSpan.Zero)
            delay = delay.Add(TimeSpan.FromDays(1));

        return delay;
    }
}
