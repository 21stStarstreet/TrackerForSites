using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TrackerForSites.Api.Data;
using TrackerForSites.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Veritabanı ──────────────────────────────────────────────────────
// Npgsql: PostgreSQL için EF Core provider.
// Connection string appsettings.json'dan okunur.
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Servisler (Dependency Injection) ────────────────────────────────
// Singleton: uygulama boyunca tek instance. Stateless servisler için.
// Scoped:    her HTTP isteği için yeni instance. DB context gibi.
// Transient: her inject için yeni instance. Hafif, stateless.

builder.Services.AddSingleton<FingerprintService>();  // Stateless, SHA256 hesaplar
builder.Services.AddSingleton<UserAgentService>();    // Parser bir kez oluşturulur
builder.Services.AddSingleton<GeoIpService>();        // Cache tutuyor, singleton olmalı
builder.Services.AddScoped<JwtService>();             // Config okur, scoped yeterli
builder.Services.AddHostedService<StatsAggregatorService>(); // Gece çalışan istatistik toplayıcı

// GeoIP için HttpClient
builder.Services.AddHttpClient("geoip", c =>
{
    c.BaseAddress = new Uri("http://ip-api.com");
    c.Timeout     = TimeSpan.FromSeconds(3); // Hızlı timeout
});

// ── JWT Kimlik Doğrulama ─────────────────────────────────────────────
// JWT (JSON Web Token): stateless authentication mekanizması.
// Kullanıcı login olur, imzalı token alır, her istekte gönderir.
// Sunucu token'ı verifiye eder, DB'ye bakmaz.
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key yapılandırması eksik!");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        // ÖNEMLI: MapInboundClaims = false
        // Varsayılan olarak ASP.NET Core, JWT claim adlarını
        // uzun WS-Federation URI'larına dönüştürür:
        //   "sub" → "http://schemas.xmlsoap.org/.../nameidentifier"
        // Bu durumda User.FindFirstValue("sub") → null döner!
        // false yapınca claim adları JWT'deki gibi kalır: "sub", "email" vb.
        opt.MapInboundClaims = false;

        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// ── CORS ──────────────────────────────────────────────────────────────
// tracker.js farklı bir domain'den istek atar.
// CORS: tarayıcının cross-origin isteklerine izin veriyoruz.
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("TrackerPolicy", policy =>
    {
        policy
            .AllowAnyOrigin()   // Herhangi bir site tracker'ı embed edebilir
            .AllowAnyMethod()
            .AllowAnyHeader();
    });

    opt.AddPolicy("DashboardPolicy", policy =>
    {
        // Dashboard için daha kısıtlı CORS
        var allowed = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? ["http://localhost:3000"];
        policy
            .WithOrigins(allowed)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks(); // /health endpoint için

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────
// Sıralama önemli! Her middleware bir sonrakini çağırır.

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

// CORS: global olarak TrackerPolicy uygula (/api/collect için)
// Dashboard endpoint'leri kendi [EnableCors] attribute'larıyla
// DashboardPolicy kullanacak.
app.UseCors("TrackerPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health"); // docker-compose healthcheck'i bunu kullanır
app.MapControllers();

app.Run();
