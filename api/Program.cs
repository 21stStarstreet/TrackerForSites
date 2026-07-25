using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using TrackerForSites.Api.Data;
using TrackerForSites.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Veritabanı ──────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Servisler (Dependency Injection) ────────────────────────────────
builder.Services.AddSingleton<FingerprintService>();
builder.Services.AddSingleton<UserAgentService>();
builder.Services.AddSingleton<GeoIpService>();
builder.Services.AddScoped<JwtService>();

// Gece 00:05 UTC'de çalışan istatistik toplayıcı
builder.Services.AddHostedService<StatsAggregatorService>();

// ── Event Kuyruğu ────────────────────────────────────────────────────
// EventQueueService hem IEventQueue (inject için) hem BackgroundService (arka plan) olarak çalışır.
// AddSingleton ile tek instance oluşturur, AddHostedService aynı instance'ı kullanır.
builder.Services.AddSingleton<EventQueueService>();
builder.Services.AddSingleton<IEventQueue>(sp => sp.GetRequiredService<EventQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<EventQueueService>());

// GeoIP için HttpClient
builder.Services.AddHttpClient("geoip", c =>
{
    c.BaseAddress = new Uri("http://ip-api.com");
    c.Timeout     = TimeSpan.FromSeconds(3);
});

// ── JWT Kimlik Doğrulama ─────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key yapılandırması eksik!");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        // MapInboundClaims = false: claim adları JWT'deki gibi kalır ("sub", "email" vb.)
        // Aksi hâlde ASP.NET Core uzun URI'lara dönüştürür ve User.FindFirstValue("sub") null döner.
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

        // SSE desteği: EventSource tarayıcı API'si Authorization başlığı gönderemez.
        // Çözüm: JWT token'ı ?token= query parametresinden oku.
        // Bu yalnızca /realtime/stream endpoint'i için gerekli.
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["token"];
                if (!string.IsNullOrEmpty(token))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Rate Limiting ────────────────────────────────────────────────────
// .NET 8 yerleşik rate limiter — ek paket gerekmez.
// Her politika IP başına uygulanır (partition key = RemoteIpAddress).
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 429 yanıtı JSON formatında döndür
    opt.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(new { message = "Çok fazla istek. Lütfen birkaç saniye bekleyin." }),
            ct);
    };

    // Collect politikası: 60 istek / dakika / IP (kayar pencere)
    // tracker.js sayfa geçişlerinde çağırır — normal kullanımda max ~20/dk.
    opt.AddPolicy("collect", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                Window           = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,  // 10'ar saniyelik dilimler
                PermitLimit      = 60,
                QueueLimit       = 0    // Sıra bekleme yok — hemen 429 dön
            }));

    // Auth politikası: 10 istek / dakika / IP (sabit pencere)
    // Brute-force şifre denemelerini engeller.
    opt.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window      = TimeSpan.FromMinutes(1),
                PermitLimit = 10,
                QueueLimit  = 0
            }));
});

// ── CORS ──────────────────────────────────────────────────────────────
builder.Services.AddCors(opt =>
{
    // tracker.js herhangi bir siteden çağırabilmeli
    opt.AddPolicy("TrackerPolicy", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    // Dashboard için kısıtlı CORS — yalnızca kayıtlı origin'ler
    opt.AddPolicy("DashboardPolicy", policy =>
    {
        var allowed = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? ["http://localhost:3000"];
        policy.WithOrigins(allowed)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────
// Sıralama önemlidir. Her middleware bir sonrakini çağırır.

// Global hata yakalayıcı — en başta olmalı (diğer middleware hatalarını da yakalar)
app.UseExceptionHandler(errorApp =>
    errorApp.Run(async context =>
    {
        context.Response.StatusCode  = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { message = "Beklenmeyen bir hata oluştu." }));
    }));

app.UseCors("TrackerPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
