using System.Security.Cryptography;
using System.Text;

namespace TrackerForSites.Api.Services;

/// <summary>
/// Cookie'siz unique visitor takibi için fingerprint üretir.
///
/// FINGERPRINT NEDİR?
/// Kullanıcının tarayıcı/cihaz özelliklerinden oluşturulan
/// anonim kimlik. Çerez gerektirmez, kişisel veri içermez.
///
/// HESAPLAMA:
/// SHA256(ip_adresi, user_agent , dil ve ekran_genişliği)
///
/// NEDEN BU 4 ALAN?
/// - ip:           aynı ağdaki kullanıcıları ayırt eder
/// - user_agent:   tarayıcı + OS versiyonunu içerir
/// - language:     ek ayırt edicilik sağlar
/// - screen_width: cihaz boyutunu yansıtır
///
/// SINIRI:
/// Aynı ağdaki iki kullanıcı aynı fingerprint'e sahip olabilir.
/// Mükemmel değil, ama cookie'siz için kabul edilebilir.
/// </summary>
public class FingerprintService
{
    /// <summary>
    /// Verilen bilgilerden fingerprint hash'i üretir.
    /// </summary>
    /// <param name="ipAddress">Ham IP (hash'lendikten sonra atılır)</param>
    /// <param name="userAgent">User-Agent string</param>
    /// <param name="language">navigator.language (örn: "tr-TR")</param>
    /// <param name="screenWidth">screen.width</param>
    /// <returns>64 karakterli SHA256 hex string</returns>
    public string Generate(string? ipAddress, string? userAgent, string? language, short? screenWidth)
    {
        // Tüm değerleri birleştir. Boş değerler için sabit placeholder.
        var raw = string.Concat(
            ipAddress   ?? "unknown",
            "|",
            userAgent   ?? "unknown",
            "|",
            language    ?? "unknown",
            "|",
            screenWidth?.ToString() ?? "0"
        );

        // SHA256: 256 bit = 32 byte = 64 hex karakter
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// IP adresinin SHA256 hash'ini üretir.
    /// Ham IP asla DB'ye yazılmaz — sadece bu hash saklanır.
    /// </summary>
    public string HashIp(string ipAddress)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Refresh token için güvenli rastgele string üretir.
    /// Bu token istemciye gönderilir, DB'ye hash'i yazılır.
    /// </summary>
    public (string token, string hash) GenerateRefreshToken()
    {
        // 64 byte = 512 bit → yeterince tahmin edilemez
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(tokenBytes);

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return (token, hash);
    }
}
