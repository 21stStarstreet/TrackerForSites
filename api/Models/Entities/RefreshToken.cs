using System.ComponentModel.DataAnnotations.Schema;

namespace TrackerForSites.Api.Models.Entities;

/// <summary>
/// JWT refresh token'larını temsil eder.
/// refresh_tokens tablosuna karşılık gelir.
///
/// Neden token'ın kendisini değil hash'ini saklıyoruz?
/// DB sızıntısı olursa bile ham token'lar kullanılamaz.
/// </summary>
[Table("refresh_tokens")]
public class RefreshToken
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    /// <summary>
    /// SHA256(token): ham token asla DB'ye yazılmaz.
    /// </summary>
    [Column("token_hash")]
    public string TokenHash { get; set; } = string.Empty;

    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// null -> aktif token
    /// dolu -> logout veya şifre değişikliğiyle iptal edildi
    /// </summary>
    [Column("revoked_at")]
    public DateTimeOffset? RevokedAt { get; set; }

    // Hesaplanmış özellik, DB'ye yazılmaz
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    // Navigation property
    public User User { get; set; } = null!;
}
