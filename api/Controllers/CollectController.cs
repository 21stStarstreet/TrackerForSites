using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TrackerForSites.Api.Data;
using TrackerForSites.Api.Models.Dtos;
using TrackerForSites.Api.Services;

namespace TrackerForSites.Api.Controllers;

/// <summary>
/// tracker.js'ten gelen event'leri alır ve arka plan kuyruğuna iletir.
///
/// ENDPOINT: POST /api/collect
///
/// AKIŞ (v2 — Background Queue):
/// 1. Body'den CollectRequest al
/// 2. api_key ile siteyi doğrula
/// 3. Bot filtresi uygula (sunucu tarafı)
/// 4. IP al
/// 5. EventQueueService'e enqueue et (non-blocking, mikrosaniye)
/// 6. 204 No Content döndür — toplam yanıt süresi ~12ms
///
/// GeoIP, fingerprint, UA parse ve DB write işlemleri EventQueueService
/// tarafından arka planda yapılır (50'li batch INSERT).
/// </summary>
[ApiController]
[Route("api")]
[EnableRateLimiting("collect")]
public class CollectController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEventQueue _queue;
    private readonly ILogger<CollectController> _logger;

    // Sunucu tarafı bot filtresi — tracker.js'i bypass eden botlar için
    private static readonly System.Text.RegularExpressions.Regex BotPattern =
        new(@"bot|crawl|spider|headless|puppet|phantom|selenium|slurp|mediapartners",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public CollectController(
        AppDbContext db,
        IEventQueue queue,
        ILogger<CollectController> logger)
    {
        _db     = db;
        _queue  = queue;
        _logger = logger;
    }

    [HttpPost("collect")]
    public async Task<IActionResult> Collect([FromBody] CollectRequest req)
    {
        try
        {
            // 1. Temel validasyon
            if (string.IsNullOrWhiteSpace(req.S) || string.IsNullOrWhiteSpace(req.U))
                return BadRequest();

            // 2. Site doğrulama — api_key geçerli mi?
            var site = await _db.Sites
                .Where(s => s.ApiKey == req.S && s.IsActive)
                .Select(s => new { s.Id })
                .FirstOrDefaultAsync();

            if (site is null)
                return Unauthorized();

            // 3. Sunucu tarafı bot filtresi
            var userAgent = Request.Headers.UserAgent.ToString();
            if (BotPattern.IsMatch(userAgent))
                return NoContent(); // Botu sessizce kabul et, kuyruğa alma

            // 4. IP adresini al
            // X-Forwarded-For: proxy/load balancer arkasındaki gerçek IP
            var ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()
                            ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                            ?? "unknown";

            // Birden fazla proxy varsa ilk IP gerçek istemcidir: "1.2.3.4, 5.6.7.8" → "1.2.3.4"
            if (ipAddress.Contains(','))
                ipAddress = ipAddress.Split(',')[0].Trim();

            // 5. Kuyruğa at — non-blocking, ~microseconds
            // EventQueueService arka planda: fingerprint üret, GeoIP sor, DB'ye yaz
            // Kuyruk doluysa (>10.000 event) en eski düşürülür — analitik için kabul edilebilir
            _queue.TryEnqueue(new EventQueueItem(site.Id, ipAddress, userAgent, req));

            _logger.LogDebug("Event kuyruğa alındı: {Site} | {Url}", req.S, req.U);

            // 6. 204 No Content: body yok, en hızlı response
            // tracker.js sendBeacon zaten response'u okumaz
            return NoContent();
        }
        catch (Exception ex)
        {
            // Hata olursa tracker.js'e 500 döndürme — site bundan etkilenmesin
            _logger.LogError(ex, "Collect endpoint hatası.");
            return NoContent();
        }
    }
}
