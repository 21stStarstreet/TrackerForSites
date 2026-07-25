using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrackerForSites.Api.Data;

namespace TrackerForSites.Api.Controllers;

/// <summary>
/// Dashboard için istatistik endpoint'leri.
///
/// GET /api/stats/{siteId}?days=30   -> özet istatistikler
/// GET /api/stats/{siteId}/realtime  -> son 5 dakikadaki aktif ziyaretçi
///
/// SORGU STRATEJİSİ:
/// - Hafif sorgular: daily_stats tablosundan okur (pre-aggregated)
/// - Gerçek zamanlı: events tablosuna son 5 dk sorgusu atar
/// </summary>
[ApiController]
[Route("api/stats")]
[Authorize]
[Microsoft.AspNetCore.Cors.EnableCors("DashboardPolicy")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;

    public StatsController(AppDbContext db) => _db = db;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);

    /// <summary>
    /// Sitenin özet istatistiklerini döndürür.
    /// </summary>
    [HttpGet("{siteId:guid}")]
    public async Task<IActionResult> GetStats(Guid siteId, [FromQuery] int days = 30)
    {
        try
        {
            // Kullanıcının bu siteye erişim hakkı var mı?
            var siteExists = await _db.Sites
                .AnyAsync(s => s.Id == siteId && s.UserId == CurrentUserId && s.IsActive);

            if (!siteExists) return NotFound();

            days = Math.Clamp(days, 1, 365); // 1-365 gün arası
            var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));

            // ── Günlük trafik (grafik için) ──────────────────────────────
            // daily_stats tablosundan okur, çok hızlı
            var dailyData = await _db.DailyStats
                .Where(d => d.SiteId == siteId && d.StatDate >= from)
                .OrderBy(d => d.StatDate)
                .Select(d => new
                {
                    date           = d.StatDate.ToString("yyyy-MM-dd"),
                    pageviews      = d.Pageviews,
                    unique_visitors = d.UniqueVisitors,
                    bounce_rate    = d.BounceRate
                })
                .ToListAsync();

            // ── Toplam özet ───────────────────────────────────────────────────
            var totalPageviews      = dailyData.Sum(d => d.pageviews);
            var totalUniqueVisitors = dailyData.Sum(d => d.unique_visitors);
            // DefaultIfEmpty: boş listede Average() InvalidOperationException fırlatır!
            var avgBounceRate = dailyData.Count > 0
                ? dailyData.Average(d => (double?)d.bounce_rate)
                : null;

            // ── events tablosundan canlı hesaplamalar ─────────────────────
            var fromTs = DateTimeOffset.UtcNow.AddDays(-days);

            // Top sayfalar
            var topPages = await _db.Events
                .Where(e => e.SiteId == siteId && e.ServerTs >= fromTs)
                .GroupBy(e => e.Url)
                .Select(g => new { url = g.Key, views = g.Count() })
                .OrderByDescending(x => x.views)
                .Take(10)
                .ToListAsync();

            // Top referrer'lar
            var topReferrers = await _db.Events
                .Where(e => e.SiteId == siteId && e.ServerTs >= fromTs
                            && e.ReferrerDomain != null)
                .GroupBy(e => e.ReferrerDomain)
                .Select(g => new { domain = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(10)
                .ToListAsync();

            // Cihaz dağılımı
            var deviceBreakdown = await _db.Events
                .Where(e => e.SiteId == siteId && e.ServerTs >= fromTs)
                .GroupBy(e => e.DeviceType)
                .Select(g => new { device = g.Key ?? "unknown", count = g.Count() })
                .ToListAsync();

            // Tarayıcı dağılımı
            var browserBreakdown = await _db.Events
                .Where(e => e.SiteId == siteId && e.ServerTs >= fromTs)
                .GroupBy(e => e.Browser)
                .Select(g => new { browser = g.Key ?? "unknown", count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(8)
                .ToListAsync();

            return Ok(new
            {
                period = new { days, from = from.ToString("yyyy-MM-dd") },
                summary = new
                {
                    total_pageviews      = totalPageviews,
                    total_unique_visitors = totalUniqueVisitors,
                    avg_bounce_rate      = avgBounceRate
                },
                daily          = dailyData,
                top_pages      = topPages,
                top_referrers  = topReferrers,
                devices        = deviceBreakdown,
                browsers       = browserBreakdown
            });
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<StatsController>>();
            logger.LogError(ex, "GetStats hatası: {SiteId}", siteId);
            return StatusCode(500, new { message = "İstatistikler yüklenirken bir hata oluştu." });
        }
    }

    /// <summary>
    /// Son 5 dakikada aktif olan unique session sayısı.
    /// Dashboard'daki "Şu an X kişi sitende" sayacı — tek seferlik polling.
    /// </summary>
    [HttpGet("{siteId:guid}/realtime")]
    public async Task<IActionResult> GetRealtime(Guid siteId)
    {
        try
        {
            var siteExists = await _db.Sites
                .AnyAsync(s => s.Id == siteId && s.UserId == CurrentUserId);
            if (!siteExists) return NotFound();

            var since = DateTimeOffset.UtcNow.AddMinutes(-5);
            var activeVisitors = await _db.Events
                .Where(e => e.SiteId == siteId && e.ServerTs >= since)
                .Select(e => e.SessionId)
                .Distinct()
                .CountAsync();

            return Ok(new { active_visitors = activeVisitors, since });
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<StatsController>>();
            logger.LogError(ex, "GetRealtime hatası: {SiteId}", siteId);
            return Ok(new { active_visitors = 0, since = DateTimeOffset.UtcNow }); // Sayacı sıfırla, hata gösterme
        }
    }

    /// <summary>
    /// Server-Sent Events: aktif ziyaretçi sayısını her 10 saniyede push'lar.
    ///
    /// EventSource tarayıcı API'si custom header (Authorization: Bearer) gönderemez.
    /// JWT token ?token= query parametresi olarak alınır.
    /// Program.cs'teki JwtBearerEvents.OnMessageReceived hook'u sayesinde
    /// [Authorize] normal çalışır.
    ///
    /// Nginx: X-Accel-Buffering: no başlığı ile response buffering devre dışı bırakılır.
    /// </summary>
    [HttpGet("{siteId:guid}/realtime/stream")]
    public async Task StreamRealtime(Guid siteId, CancellationToken ct)
    {
        // Kullanıcının bu siteye erişim hakkı var mı?
        var siteExists = await _db.Sites
            .AnyAsync(s => s.Id == siteId && s.UserId == CurrentUserId && s.IsActive, ct);

        if (!siteExists)
        {
            Response.StatusCode = 404;
            return;
        }

        // SSE protokol başlıkları
        Response.Headers["Content-Type"]      = "text/event-stream";
        Response.Headers["Cache-Control"]     = "no-cache";
        Response.Headers["Connection"]        = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no"; // Nginx proxy_buffering'i bu response için devre dışı bırak

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var since = DateTimeOffset.UtcNow.AddMinutes(-5);
                var count = await _db.Events
                    .Where(e => e.SiteId == siteId && e.ServerTs >= since)
                    .Select(e => e.SessionId)
                    .Distinct()
                    .CountAsync(ct);

                // SSE format: "data: <değer>\n\n"
                await Response.WriteAsync($"data: {count}\n\n", ct);
                await Response.Body.FlushAsync(ct);

                // 10 saniyede bir güncelle
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client bağlantıyı kesti veya token süresi doldu — normal durum
        }
    }
}
