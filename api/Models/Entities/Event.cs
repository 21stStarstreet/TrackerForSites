using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrackerForSites.Api.Models.Entities;

/// <summary>
/// Her pageview veya custom event = 1 satır.
/// events tablosuna karşılık gelir.
///
/// Alan adları DB kolonlarıyla birebir eşleşir.
/// İstemciden gelen alanlar ve sunucunun eklediği alanlar bir arada.
/// </summary>
[Table("events")]
public class Event
{
    [Column("id")]
    public long Id { get; set; }

    [Column("site_id")]
    public Guid SiteId { get; set; }

    // ── İstemciden gelen alanlar ───────────────────────────────────

    /// <summary>"pageview" veya custom event adı</summary>
    [Column("event_type")]
    [MaxLength(50)]
    public string EventType { get; set; } = "pageview";

    /// <summary>pathname + search (örn: /blog?page=2)</summary>
    [Column("url")]
    [Required]
    public string Url { get; set; } = string.Empty;

    [Column("referrer")]
    public string? Referrer { get; set; }

    /// <summary>
    /// "https://google.com/search?q=..." -> "google.com"
    /// API insert sırasında Referrer'dan türetir, bir kez hesaplanır.
    /// </summary>
    [Column("referrer_domain")]
    public string? ReferrerDomain { get; set; }

    [Column("page_title")]
    public string? PageTitle { get; set; }

    [Column("language")]
    [MaxLength(10)]
    public string? Language { get; set; }

    [Column("screen_width")]
    public short? ScreenWidth { get; set; }

    [Column("session_id")]
    [Required]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// tracker.js Date.now() gönderir (Unix ms, long).
    /// API: DateTimeOffset.FromUnixTimeMilliseconds(ts)
    /// </summary>
    [Column("client_ts")]
    public DateTimeOffset? ClientTs { get; set; }

    // ── Sunucunun eklediği alanlar ─────────────────────────────────

    /// <summary>SHA256(ip): ham IP asla saklanmaz (GDPR/KVKK)</summary>
    [Column("ip_hash")]
    [MaxLength(64)]
    public string? IpHash { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// SHA256(ip, user_agent , language ve screen_width)
    /// "Unique visitor" sayımında kullanılır. Bu sayede cookie'siz takip sağlanır.
    /// </summary>
    [Column("fingerprint")]
    [Required, MaxLength(64)]
    public string Fingerprint { get; set; } = string.Empty;

    // UA'dan parse edilen alanlar, insert'te bir kez hesaplanır.

    [Column("browser")]
    [MaxLength(50)]
    public string? Browser { get; set; }

    [Column("os")]
    [MaxLength(50)]
    public string? Os { get; set; }

    /// <summary>desktop | mobile | tablet</summary>
    [Column("device_type")]
    [MaxLength(10)]
    public string? DeviceType { get; set; }

    [Column("country_code")]
    [MaxLength(2)]
    public string? CountryCode { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("server_ts")]
    public DateTimeOffset ServerTs { get; set; } = DateTimeOffset.UtcNow;

    // Navigation property
    public Site Site { get; set; } = null!;
}
