using Microsoft.EntityFrameworkCore;
using TrackerForSites.Api.Models.Entities;

namespace TrackerForSites.Api.Data;

/// <summary>
/// EF Core DbContext: uygulamanın veritabanı bağlantı noktası.
///
/// EF Core nedir?
/// ORM (Object-Relational Mapper): C# nesnelerini SQL tablolarına eşler.
/// Yazdığımız LINQ sorgularını SQL sorgularına dönüştürülür.
/// Ham SQL yazmak yerine tip-güvenli C# kodu yazarız.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Her DbSet = bir tablo. EF Core bu property'leri DB'ye bağlar.
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<DailyStat> DailyStats => Set<DailyStat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ────────────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();

            // Bir kullanıcının birden fazla sitesi olabilir
            e.HasMany(u => u.Sites)
             .WithOne(s => s.User)
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(u => u.RefreshTokens)
             .WithOne(r => r.User)
             .HasForeignKey(r => r.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── RefreshToken ─────────────────────────────────────────────
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(r => r.TokenHash).IsUnique();
            e.HasIndex(r => r.ExpiresAt); // Süresi dolmuş token temizleme
            e.HasIndex(r => r.UserId);

            // IsActive hesaplanmış, DB'ye yazılmaz
            e.Ignore(r => r.IsActive);
        });

        // ── Site ─────────────────────────────────────────────────────
        modelBuilder.Entity<Site>(e =>
        {
            e.HasIndex(s => s.Domain).IsUnique();
            e.HasIndex(s => s.ApiKey).IsUnique();
            e.HasIndex(s => s.UserId);

            e.HasMany(s => s.Events)
             .WithOne(ev => ev.Site)
             .HasForeignKey(ev => ev.SiteId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(s => s.DailyStats)
             .WithOne(d => d.Site)
             .HasForeignKey(d => d.SiteId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Event ─────────────────────────────────────────────────────
        modelBuilder.Entity<Event>(e =>
        {
            // Bileşik indeks: site_id ve server_ts -> en sık sorgu
            e.HasIndex(ev => new { ev.SiteId, ev.ServerTs });

            // Unique visitor sayımı
            e.HasIndex(ev => new { ev.SiteId, ev.Fingerprint });

            // Session bazlı sorgular
            e.HasIndex(ev => ev.SessionId);

            // Sayfa bazlı analiz
            e.HasIndex(ev => new { ev.SiteId, ev.Url });

            // Cihaz dağılımı
            e.HasIndex(ev => new { ev.SiteId, ev.DeviceType });
        });

        // ── DailyStat ─────────────────────────────────────────────────
        modelBuilder.Entity<DailyStat>(e =>
        {
            // Bir günde bir özet
            e.HasIndex(d => new { d.SiteId, d.StatDate }).IsUnique();
        });
    }
}
