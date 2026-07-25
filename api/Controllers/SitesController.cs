using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrackerForSites.Api.Data;
using TrackerForSites.Api.Models.Entities;

namespace TrackerForSites.Api.Controllers;

/// <summary>
/// Site yönetimi: kullanıcının sitelerini listele, ekle, sil.
///
/// GET    /api/sites        -> kullanıcının sitelerini listele
/// POST   /api/sites        -> yeni site ekle
/// DELETE /api/sites/{id}   -> site sil (soft delete)
///
/// Tüm endpoint'ler [Authorize]
/// Kullanıcı sadece kendi sitelerini görebilir.
/// </summary>
[ApiController]
[Route("api/sites")]
[Authorize]
[Microsoft.AspNetCore.Cors.EnableCors("DashboardPolicy")]
public class SitesController : ControllerBase
{
    private readonly AppDbContext _db;

    public SitesController(AppDbContext db) => _db = db;

    // JWT token'dan giriş yapan kullanıcının ID'sini al
    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);

    /// <summary>Kullanıcının tüm sitelerini listele.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var sites = await _db.Sites
                .Where(s => s.UserId == CurrentUserId && s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new { s.Id, s.Name, s.Domain, s.ApiKey, s.CreatedAt })
                .ToListAsync();
            return Ok(sites);
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<SitesController>>();
            logger.LogError(ex, "GetAll hatası.");
            return StatusCode(500, new { message = "Siteler yüklenirken bir hata oluştu." });
        }
    }

    /// <summary>Yeni site ekle. Domain daha önce silinmişse reaktive et.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSiteRequest req)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Domain))
                return BadRequest(new { message = "Site adı ve domain zorunludur." });

            var domain = req.Domain.Trim().ToLowerInvariant();

            // 1. Bu kullanıcıda aktif olarak kayıtlı aynı domain var mı?
            var activeSite = await _db.Sites.FirstOrDefaultAsync(s =>
                s.Domain == domain && s.IsActive && s.UserId == CurrentUserId);
            if (activeSite is not null)
                return Conflict(new { message = "Bu domain zaten kayıtlı." });

            // 2. Daha önce silinmiş (soft delete) aynı domain var mı?
            // Varsa yeniden oluşturmak yerine reaktive et.
            // API key korunur → eski embed script çalışmaya devam eder.
            var deletedSite = await _db.Sites.FirstOrDefaultAsync(s =>
                s.Domain == domain && !s.IsActive && s.UserId == CurrentUserId);

            Site site;
            if (deletedSite is not null)
            {
                deletedSite.IsActive = true;
                deletedSite.Name     = req.Name.Trim();
                site = deletedSite;
            }
            else
            {
                site = new Site
                {
                    UserId = CurrentUserId,
                    Name   = req.Name.Trim(),
                    Domain = domain,
                };
                _db.Sites.Add(site);
            }

            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new
            {
                site.Id,
                site.Name,
                site.Domain,
                site.ApiKey,
            });
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<SitesController>>();
            logger.LogError(ex, "Site oluşturma hatası.");
            return StatusCode(500, new { message = "Site eklenirken bir hata oluştu." });
        }
    }

    /// <summary>Site sil (soft delete, veri korunur).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var site = await _db.Sites
                .Where(s => s.Id == id && s.UserId == CurrentUserId)
                .FirstOrDefaultAsync();

            if (site is null) return NotFound();

            // Fiziksel silme değil, is_active=false yapıyoruz.
            // Geçmiş event verisi korunur, sadece site pasifleşir.
            site.IsActive = false;
            await _db.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<SitesController>>();
            logger.LogError(ex, "Delete hatası: {Id}", id);
            return StatusCode(500, new { message = "Site silinirken bir hata oluştu." });
        }
    }
}

public record CreateSiteRequest(string Name, string Domain);
