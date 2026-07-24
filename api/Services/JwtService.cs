using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TrackerForSites.Api.Models.Entities;

namespace TrackerForSites.Api.Services;

/// <summary>
/// JWT access token üretir ve doğrular.
///
/// JWT NASIL ÇALIŞIR?
/// 3 parçadan oluşur: Header.Payload.Signature
///
///   Header:    algoritma bilgisi (HS256)
///   Payload:   kullanıcı bilgileri, şifrelenmez, sadece imzalanır!
///   Signature: Header ve Payload -> gizli key ile imzalanır.
///
/// Sunucu token'ı doğrularken:
///   1. Signature'ı kendi key'iyle tekrar hesaplar.
///   2. Gelen signature ile karşılaştırır.
///   3. Eşleşirse -> token geçerli, payload'a güvenilir.
///
/// ÖNEMLİ: Payload base64 ile encode edilir, şifrelenmez.
/// Payload'a şifre, kredi kartı gibi hassas veri koyma!
/// </summary>
public class JwtService
{
    private readonly IConfiguration _config;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtService(IConfiguration config)
    {
        _config = config;
        var key = config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key eksik!");

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    /// <summary>
    /// Kullanıcı için kısa ömürlü access token üretir (varsayılan: 15 dk).
    /// </summary>
    public (string token, DateTimeOffset expiresAt) GenerateAccessToken(User user)
    {
        var expiryMinutes = _config.GetValue<int>("Jwt:AccessTokenExpiryMinutes", 15);
        var expiresAt     = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        // Claims: token'ın içine gömdüğümüz bilgiler.
        // MapInboundClaims = false olduğu için JWT isimleriyle okunur:
        //   User.FindFirstValue(JwtRegisteredClaimNames.Sub) -> kullanıcı ID
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name,  user.FullName ?? user.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()), // Token ID
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>
    /// Token'dan kullanıcı ID'sini okur.
    /// Süresi dolmuş token'ları da okuyabiliriz (refresh için gerekli).
    /// </summary>
    public Guid? GetUserIdFromExpiredToken(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = _signingKey,
            ValidateIssuer           = true,
            ValidIssuer              = _config["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = _config["Jwt:Audience"],
            ValidateLifetime         = false, // Süresi dolmuş olabilir!
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, parameters, out _);

            var idStr = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(idStr, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }
}
