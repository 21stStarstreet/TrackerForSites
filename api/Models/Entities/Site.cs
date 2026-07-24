using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrackerForSites.Api.Models.Entities;

[Table("sites")]
public class Site
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("name")]
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Column("domain")]
    [Required, MaxLength(255)]
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// tracker.js'teki data-site-id attribute değeri bu olacak.
    /// Her /collect isteğinde bu key ile site doğrulanır.
    /// </summary>
    [Column("api_key")]
    public string ApiKey { get; set; } = Guid.NewGuid().ToString();

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<Event> Events { get; set; } = [];
    public ICollection<DailyStat> DailyStats { get; set; } = [];
}
