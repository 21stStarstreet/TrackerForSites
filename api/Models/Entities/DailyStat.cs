using System.ComponentModel.DataAnnotations.Schema;

namespace TrackerForSites.Api.Models.Entities;

[Table("daily_stats")]
public class DailyStat
{
    [Column("id")]
    public long Id { get; set; }

    [Column("site_id")]
    public Guid SiteId { get; set; }

    [Column("stat_date")]
    public DateOnly StatDate { get; set; }

    [Column("pageviews")]
    public int Pageviews { get; set; }

    [Column("unique_visitors")]
    public int UniqueVisitors { get; set; }

    [Column("unique_sessions")]
    public int UniqueSessions { get; set; }

    /// <summary>
    /// 0.000 - 1.000 (örn: 0.42 = %42 bounce)
    /// Sadece 1 sayfa görüp giden oturum oranı.
    /// </summary>
    [Column("bounce_rate")]
    public decimal? BounceRate { get; set; }

    /// <summary>
    /// [{"url": "/blog", "views": 142}, ...]
    /// JSONB olarak saklanır, dashboard direkt okur.
    /// </summary>
    [Column("top_pages", TypeName = "jsonb")]
    public string? TopPages { get; set; }

    [Column("top_referrers", TypeName = "jsonb")]
    public string? TopReferrers { get; set; }

    [Column("country_breakdown", TypeName = "jsonb")]
    public string? CountryBreakdown { get; set; }

    [Column("browser_breakdown", TypeName = "jsonb")]
    public string? BrowserBreakdown { get; set; }

    [Column("device_breakdown", TypeName = "jsonb")]
    public string? DeviceBreakdown { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation property
    public Site Site { get; set; } = null!;
}
