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
        var sites = await _db.Sites
            .Where(s => s.UserId == CurrentUserId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Domain,
                s.ApiKey,
                s.CreatedAt
            })
            .ToListAsync();

        return Ok(sites);
    }

    /// <summary>Yeni site ekle.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSiteRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Domain))
            return BadRequest(new { message = "Site adı ve domain zorunludur." });

        // Aynı domain başka kullanıcıda var mı?
        var exists = await _db.Sites.AnyAsync(s => s.Domain == req.Domain);
        if (exists)
            return Conflict(new { message = "Bu domain zaten kayıtlı." });

        var site = new Site
        {
            UserId = CurrentUserId,
            Name   = req.Name.Trim(),
            Domain = req.Domain.Trim().ToLowerInvariant(),
        };

        _db.Sites.Add(site);
        await _db.SaveChangesAsync();

        // Embed kodu ile birlikte döndür
        return CreatedAtAction(nameof(GetAll), new
        {
            site.Id,
            site.Name,
            site.Domain,
            site.ApiKey,
            EmbedCode = $"""<script async defer src="https://yourdomain.com/tracker.js" data-site-id="{site.ApiKey}"></script>"""
        });
    }

    /// <summary>Site sil (soft delete, veri korunur).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var site = await _db.Sites
            .Where(s => s.Id == id && s.UserId == CurrentUserId)
            .FirstOrDefaultAsync();

        if (site is null)
            return NotFound();

        // Fiziksel silme değil, is_active=false yapıyoruz.
        // Geçmiş event verisi korunur, sadece site pasifleşir.
        site.IsActive = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}

public record CreateSiteRequest(string Name, string Domain);
