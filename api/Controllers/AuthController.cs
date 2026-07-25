using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TrackerForSites.Api.Data;
using TrackerForSites.Api.Models.Dtos;
using TrackerForSites.Api.Models.Entities;
using TrackerForSites.Api.Services;

namespace TrackerForSites.Api.Controllers;

/// <summary>
/// Kimlik doğrulama işlemleri.
///
/// POST /api/auth/login    -> email+şifre -> access token ve refresh token
/// POST /api/auth/refresh  -> refresh token -> yeni access token
/// POST /api/auth/logout   -> refresh token'ı iptal et
///
/// TOKEN AKIŞI:
/// 1. Login -> access token (15 dk) + refresh token (30 gün) döner
/// 2. access token süresi dolunca -> /refresh ile yenile
/// 3. Logout -> refresh token DB'de revoke edilir
///    -> artık yeni access token üretilemez
/// </summary>
[ApiController]
[Route("api/auth")]
[Microsoft.AspNetCore.Cors.EnableCors("DashboardPolicy")]
[EnableRateLimiting("auth")] // Brute-force koruması: 10 istek/dakika/IP
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    private readonly FingerprintService _fingerprint;
    private readonly IConfiguration _config;

    public AuthController(
        AppDbContext db,
        JwtService jwt,
        FingerprintService fingerprint,
        IConfiguration config)
    {
        _db          = db;
        _jwt         = jwt;
        _fingerprint = fingerprint;
        _config      = config;
    }

    /// <summary>
    /// Email ve şifre ile giriş.
    /// Başarılı -> access token -> refresh token döner.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        try
        {
        // 1. Kullanıcıyı bul
        var user = await _db.Users
            .Where(u => u.Email == req.Email && u.IsActive)
            .FirstOrDefaultAsync();

        // 2. Şifre doğrula
        // Timing attack önlemi: BCrypt.Verify her zaman çalışmalı.
        // user null iken && short-circuit'i Verify'ı atlarsa:
        //   - email var -> yanit ~200ms (BCrypt çaldı)
        //   - email yok -> yanıt ~1ms  (BCrypt atlandı)
        // Bu fark ölçülebilir -> geçerli email tespiti mümkün olur.
        // Çözüm: her zaman BCrypt çalıştır, sonra user varlığını kontrol et.
        var hashToVerify = user?.PasswordHash
            ?? "$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj/pg6mDDSJy"; // dummy
        var passwordMatch = BCrypt.Net.BCrypt.Verify(req.Password, hashToVerify);
        var passwordValid = user is not null && passwordMatch;

        if (!passwordValid)
        {
            // Kasıtlı belirsiz mesaj: hangi alanın yanlış olduğunu söyleme.
            return Unauthorized(new { message = "E-posta veya şifre hatalı." });
        }

        // 3. Token'ları üret
        var (accessToken, expiresAt) = _jwt.GenerateAccessToken(user!);
        var (refreshToken, refreshHash) = _fingerprint.GenerateRefreshToken();

        // 4. Refresh token'ı DB'ye kaydet (hash olarak)
        var expiryDays = _config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 30);
        var rt = new RefreshToken
        {
            UserId    = user!.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(expiryDays),
            UserAgent = Request.Headers.UserAgent.ToString(),
        };

        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync();

        return Ok(new LoginResponse(
            AccessToken:  accessToken,
            RefreshToken: refreshToken,
            ExpiresAt:    expiresAt,
            FullName:     user.FullName ?? user.Email,
            Email:        user.Email
        ));
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<AuthController>>();
            logger.LogError(ex, "Login endpoint hatası.");
            return StatusCode(500, new { message = "Giriş işlemi sırasında bir hata oluştu." });
        }
    }

    /// <summary>
    /// Refresh token ile yeni access token al.
    /// Access token süresi dolduğunda frontend bunu çağırır.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        try
        {
        // 1. Gelen refresh token'ı hash'le ve DB'de ara
        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(req.RefreshToken));
        var tokenHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var storedToken = await _db.RefreshTokens
            .Include(r => r.User)
            .Where(r => r.TokenHash == tokenHash)
            .FirstOrDefaultAsync();

        // 2. Token geçerli mi?
        if (storedToken is null || !storedToken.IsActive)
            return Unauthorized(new { message = "Geçersiz veya süresi dolmuş token." });

        if (!storedToken.User.IsActive)
            return Unauthorized(new { message = "Hesap devre dışı." });

        // 3. Eski token'ı iptal et (refresh token rotation)
        // Her kullanımda yeni bir refresh token üretiyoruz.
        // Neden? Token çalınırsa eski token geçersiz kalır.
        storedToken.RevokedAt = DateTimeOffset.UtcNow;

        // 4. Yeni token'ları üret
        var (newAccessToken, expiresAt) = _jwt.GenerateAccessToken(storedToken.User);
        var (newRefreshToken, newRefreshHash) = _fingerprint.GenerateRefreshToken();

        var expiryDays = _config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 30);
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId    = storedToken.UserId,
            TokenHash = newRefreshHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(expiryDays),
            UserAgent = Request.Headers.UserAgent.ToString(),
        });

        await _db.SaveChangesAsync();

        return Ok(new LoginResponse(
            AccessToken:  newAccessToken,
            RefreshToken: newRefreshToken,
            ExpiresAt:    expiresAt,
            FullName:     storedToken.User.FullName ?? storedToken.User.Email,
            Email:        storedToken.User.Email
        ));
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<AuthController>>();
            logger.LogError(ex, "Refresh endpoint hatası.");
            return StatusCode(500, new { message = "Token yenileme sırasında bir hata oluştu." });
        }
    }

    /// <summary>
    /// Çıkış yapılırsa refresh token'ı geçersiz kıl.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(req.RefreshToken));
        var tokenHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var token = await _db.RefreshTokens
            .Where(r => r.TokenHash == tokenHash && r.RevokedAt == null)
            .FirstOrDefaultAsync();

        if (token is not null)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }

        // Token bulunsun ya da bulunmasın 204 döndür.
        // Neden? Logout idempotent olmalı, iki kez çağrılması sorun olmamalı.
        return NoContent();
    }
}
