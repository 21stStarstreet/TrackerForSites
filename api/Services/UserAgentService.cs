using UAParser;

namespace TrackerForSites.Api.Services;

/// <summary>
/// User-Agent string'ini parse ederek tarayıcı, OS ve cihaz bilgisi çıkarır.
///
/// NEDEN INSERT'TE PARSE EDİYORUZ?
/// Dashboard her açıldığında milyonlarca UA string'ini parse etmek
/// imkânsız derecede yavaş olur. Bir kez hesaplayıp saklarız.
///
/// KULLANDIĞIMIZ KÜTÜPHANE: UAParser (NuGet: UAParser 3.1.47)
/// ua-parser projesi tarafından desteklenen regex tabanlı parser.
/// </summary>
public class UserAgentService
{
    // Parser oluşturmak pahalı, singleton olarak bir kez yaratıyoruz.
    // DI container zaten singleton olarak register edeceğiz.
    private readonly Parser _parser = Parser.GetDefault();

    public ParsedUserAgent Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return new ParsedUserAgent("Unknown", "Unknown", "desktop");

        var client = _parser.Parse(userAgent);

        var browser = client.UA.Family;     // "Chrome", "Firefox", "Safari"
        var os      = client.OS.Family;     // "Windows", "macOS", "Android"

        // Cihaz tipi: UAParser'ın device.family'si bazen "Other" döner.
        // Daha güvenilir yöntem: OS ailesine bakarak sınıflandırma.
        var device = DetectDeviceType(client.OS.Family, client.Device.Family);

        return new ParsedUserAgent(browser, os, device);
    }

    private static string DetectDeviceType(string osFamily, string deviceFamily)
    {
        // Mobil OS'lar
        if (osFamily is "Android" or "iOS" or "Windows Phone" or "BlackBerry OS")
        {
            // iPad vs iPhone ayrımı
            return deviceFamily.Contains("iPad", StringComparison.OrdinalIgnoreCase)
                ? "tablet"
                : "mobile";
        }

        // Tablet sinyalleri
        if (deviceFamily.Contains("Tablet", StringComparison.OrdinalIgnoreCase) ||
            deviceFamily.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            return "tablet";

        // Geri kalan her şey desktop
        return "desktop";
    }
}

/// <summary>
/// UserAgentService'in döndürdüğü sonuç.
/// Record: immutable, value-based equality, kısa sözdizimi.
/// </summary>
public record ParsedUserAgent(string Browser, string Os, string DeviceType);
