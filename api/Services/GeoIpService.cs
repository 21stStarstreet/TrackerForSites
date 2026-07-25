using System.Collections.Concurrent;

namespace TrackerForSites.Api.Services;

/// <summary>
/// IP adresinden ülke ve şehir bilgisi çıkarır.
///
/// GeoIP NASIL ÇALIŞIR?
/// IP adresleri belirli coğrafi bölgelere tahsis edilmiştir.
/// Bu tahsis bilgisi bir veritabanında tutulur.
///
/// SEÇENEKLER:
/// 1. ip-api.com (ücretsiz, saniyede 45 istek limit) -> bu projede kullanıyoruz
/// 2. MaxMind GeoLite2 (ücretsiz DB, local sorgu, çok daha hızlı)
/// 3. ipinfo.io, ipstack.com (ücretli, yüksek limit)
/// </summary>
public class GeoIpService
{
    private readonly HttpClient _http;
    private readonly ILogger<GeoIpService> _logger;

    // Basit in-memory cache: aynı IP'yi tekrar sorgulamayalım.
    // Production'da Redis veya IMemoryCache daha uygun.
    private readonly ConcurrentDictionary<string, GeoIpResult> _cache = new();

    public GeoIpService(IHttpClientFactory httpFactory, ILogger<GeoIpService> logger)
    {
        _http   = httpFactory.CreateClient("geoip");
        _logger = logger;
    }

    /// <summary>
    /// IP adresinden ülke kodu ve şehir bilgisi getirir.
    /// Hata durumunda null döner, event kaydı yine de yapılır.
    /// </summary>
    public async Task<GeoIpResult?> LookupAsync(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || IsPrivateIp(ipAddress))
            return null;

        // Cache'de var mı?
        if (_cache.TryGetValue(ipAddress, out var cached))
            return cached;

        try
        {
            // ip-api.com/json/{ip}?fields=countryCode,city
            var response = await _http.GetFromJsonAsync<IpApiResponse>(
                $"/json/{ipAddress}?fields=status,countryCode,city"
            );

            if (response?.Status != "success")
                return null;

            var result = new GeoIpResult(response.CountryCode, response.City);

            // Cache'e ekle. Sınırsız büyümeyi önlemek için: 5000 girişi aşınca temizle.
            if (_cache.Count >= 5_000) _cache.Clear();
            _cache[ipAddress] = result;

            return result;
        }
        catch (Exception ex)
        {
            // GeoIP hatası event kaydını engellemez, sadece loglanır
            _logger.LogWarning(ex, "GeoIP lookup failed for {Ip}", ipAddress);
            return null;
        }
    }

    /// <summary>
    /// Private/local IP adreslerini GeoIP sorgusundan muaf tutar.
    /// (127.0.0.1, 192.168.x.x, 10.x.x.x, ::1 vb.)
    /// </summary>
    private static bool IsPrivateIp(string ip)
    {
        return ip is "127.0.0.1" or "::1" or "localhost" ||
               ip.StartsWith("192.168.") ||
               ip.StartsWith("10.")      ||
               ip.StartsWith("172.16.") ||
               ip.StartsWith("172.17.") ||
               ip.StartsWith("172.18.") ||
               ip.StartsWith("172.19.") ||
               ip.StartsWith("172.2")   ||
               ip.StartsWith("172.3");
    }

    // ip-api.com'un JSON response modeli
    private record IpApiResponse(string? Status, string? CountryCode, string? City);
}

public record GeoIpResult(string? CountryCode, string? City);
