using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrackerForSites.Api.Models.Entities;

/// <summary>
/// Dashboard'a giriş yapan kullanıcıyı temsil eder.
/// users tablosuna karşılık gelir.
/// </summary>
[Table("users")]
public class User
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("email")]
    [Required, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Düz metin şifre ASLA burada olmaz.
    /// BCrypt.Net ile hash'lenmiş değer saklanır.
    /// </summary>
    [Column("password_hash")]
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("full_name")]
    public string? FullName { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties: EF Core bu ilişkileri yönetir
    public ICollection<Site> Sites { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
