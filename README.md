# TrackerForSites

Gizlilik odaklı, cookie'siz, kendi sunucunda barındırılan web analitik platformu.  
Üçüncü taraf servise bağımlılık yok. Ziyaretçi verisi tamamen sende kalır.

---

## 💡 Nasıl Çalışır?

Docker yüklü herhangi bir sunucuya kopyala, `.env` ayarlarını yap ve başlat, hepsi bu.  
Veritabanı, API ve arayüz tek bir komutla birlikte ayağa kalkar.

Çalışmaya başlayınca izlemek istediğin sitenin `<head>` bölümüne tek satır `<script>` eklersin, veriler anında akmaya başlar.

---

## 📋 İçindekiler

- [Özellikler](#özellikler)
- [Mimari](#mimari)
- [Başlangıç — Production (Docker)](#başlangıç--production-docker)
- [Geliştirme Ortamı](#geliştirme-ortamı)
- [Konfigürasyon Referansı](#konfigürasyon-referansı)
- [Proje Yapısı](#proje-yapısı)
- [Güvenlik](#güvenlik)

---

## ✨ Özellikler

<img width="1919" height="991" alt="image" src="https://github.com/user-attachments/assets/2c1e923f-8c0d-408f-ac21-da352ebd1c07" />

| Özellik | Açıklama |
|---|---|
| ⚡ Sıfır performans etkisi | **< 1 KB** tracker, `async defer` ile yüklenir — sayfayı asla bloklamaz |
| 🍪 Cookie'siz | Tarayıcı fingerprint ile unique visitor takibi |
| 🔒 Gizlilik öncelikli | Ham IP asla saklanmaz — sadece SHA256 hash |
| 📊 Gerçek zamanlı | Son 5 dakikadaki aktif ziyaretçi sayacı |
| 🌐 Çoklu site | Tek dashboard'dan tüm sitelerini yönet |
| 📱 Cihaz analizi | Masaüstü / Mobil / Tablet dağılımı |
| 🗺️ Coğrafi analiz | Ülke bazlı trafik (ip-api.com) |
| 🤖 Bot filtreleme | Hem istemci hem sunucu tarafında |
| 🌙 Dark / Light mod | Sistem temasına uyumlu |
| 🐳 Docker hazır | Tek komutla production ortamı |

---

## 🏗️ Mimari

```
Ziyaretçi Tarayıcısı
       │
       │  <script src="/tracker.js">
       │
       ▼
┌─────────────────┐     POST /api/collect      ┌──────────────────┐
│   tracker.js    │ ─────────────────────────► │   .NET 8 API     │
│ (sendBeacon)    │                             │  • Fingerprint   │
└─────────────────┘                             │  • Bot filtresi  │
                                                │  • GeoIP         │
Dashboard Kullanıcısı                           │  • Aggregator    │
       │                                        └────────┬─────────┘
       │  HTTP + JWT                                     │
       ▼                                                 ▼
┌─────────────────┐     GET /api/stats         ┌──────────────────┐
│ React Dashboard │ ◄───────────────────────── │   PostgreSQL 16  │
│  (Vite + Nginx) │                             │  events / stats  │
└─────────────────┘                             └──────────────────┘
```

- `tracker.js` izlemek istediğin siteye `<script>` olarak eklenir
- Her sayfa görüntülemesinde `POST /api/collect` çağrısı yapılır
- API fingerprint üretir, bot kontrolü yapar, eventi kaydeder
- Her gece 00:05 UTC'de arka plan servisi günlük özetleri hesaplar
- Dashboard, JWT ile oturum açarak istatistikleri çeker

---

## ⚡ tracker.js — Sıfır Performans Etkisi

Projenin temel prensibi: izleme kodu ziyaretçinin deneyimini **hiçbir şekilde** yavaşlatmamalı.

**889 byte** — gzip sonrası ~500 byte. Karşılaştırma için: Google Analytics ~45 KB.

| Teknik | Açıklama |
|---|---|
| `async defer` | Script sayfanın render'ını bloklamaz, arka planda indirilir |
| `navigator.sendBeacon` | Veri gönderimi non-blocking'dir — kullanıcı bir sonraki sayfaya geçse de istek tamamlanır |
| Bot erken çıkış | Bot tespit edilirse ilk satırda `return` — API'ye hiç istek atılmaz |
| IIFE | Global scope kirlenmez, diğer scriptlerle çakışma olmaz |
| SPA desteği | `history.pushState` monkey-patch ile React/Vue/Next.js'de sayfa geçişleri takip edilir |
| Minimum payload | Yalnızca 8 alan gönderilir — gereksiz veri yok |
| Cookie yok | `sessionStorage` kullanılır, tarayıcıya hiçbir şey yazılmaz |

---

## 🚀 Başlangıç — Production (Docker)

### Ön koşul
Docker yüklü bir sunucu gerektirir. Başka hiçbir şey gerekmez.

---

**1. Depoyu indir**
```bash
git clone https://github.com/kullaniciadi/trackerforsite.git
cd trackerforsite
```

**2. `.env` dosyasını oluştur**
```bash
cp .env.example .env
```

`.env` dosyasını aç. **Değiştirilmesi gereken 3 yer var:**

| Değişken | Ne yazacaksın |
|---|---|
| `JWT_SECRET` | Rastgele, uzun bir metin — örnek: `openssl rand -base64 48` çıktısı |
| `DASHBOARD_ORIGIN` | Dashboard'unun yayınlandığı URL — örn: `https://analytics.siten.com` |
| `TRACKER_URL` | tracker.js'in tam URL'i — örn: `https://analytics.siten.com/tracker.js` |

> `DB_PASSWORD` varsayılan olarak `postgres` kalabilir. İstersen değiştir.

**3. Başlat**
```bash
docker compose up -d
```

Tüm servisler (veritabanı, API, dashboard) ayağa kalkar. Veritabanı şeması ilk açılışta otomatik oluşturulur.

---

**4. İlk kullanıcını oluştur**

Sisteme girebilmek için veritabanına bir kullanıcı eklemen gerekiyor.

Önce şifrenin BCrypt hash'ini üret:  
→ **[bcrypt.online](https://bcrypt.online)** adresine git, şifreni yaz, **Cost = 12** seç ve oluştur.  
Çıkan değer `$2a$12$...` veya `$2b$12$...` ile başlamalıdır.

Ardından veritabanına ekle:

```bash
docker compose exec db psql -U postgres -d trackerdb
```

```sql
INSERT INTO users (email, password_hash, full_name)
VALUES (
  'sen@email.com',
  '$2a$12$buraya-uretilen-hash-gelecek',
  'Adın Soyadın'
);
```

**5. Giriş yap ve siteyi ekle**

Tarayıcıda `http://sunucu-ip-adresi` adresine git.  
4. adımda girdiğin **e-posta** ve **şifre** ile giriş yap → **Sites** bölümünden yeni site ekle → embed kodunu kopyala.

**6. Siteyi izlemeye başla**

Dashboard'da oluşan embed kodunu izlemek istediğin web sitesinin `<head>` bölümüne yapıştır:

```html
<head>
  ...
  <script async defer
    src="https://analytics.siten.com/tracker.js"
    data-site-id="SANA-VERILEN-API-KEY">
  </script>
</head>
```

---

## 🛠️ Geliştirme Ortamı

### Ön koşullar

| Araç | Sürüm |
|---|---|
| .NET SDK | 8.0+ |
| Node.js | 20+ |
| PostgreSQL | 14+ |

### 1. Veritabanı

```bash
psql -U postgres -c "CREATE DATABASE trackerdb;"
psql -U postgres -d trackerdb -f db/schema.sql

# İsteğe bağlı: test kullanıcısı ve örnek site ekle
# Kullanıcı: admin@trackerforsite.com / Şifre: password123
psql -U postgres -d trackerdb -f db/seed.sql
```

### 2. API

```bash
cd api
dotnet run
# http://localhost:5000
```

`api/appsettings.json` içindeki `Jwt:Key` değerini istediğin bir string ile değiştir (geliştirme için kısa olabilir).

### 3. Dashboard

```bash
cd dashboard
npm install
npm run dev
# http://localhost:3000
```

Vite proxy'si sayesinde `/api` istekleri otomatik olarak `localhost:5000`'e yönlendirilir — CORS ayarı yapman gerekmez.

### 4. tracker.js'i Test Et

```html
<script async defer
  src="http://localhost:3000/tracker.js"
  data-site-id="test-key-local">
</script>
```

`data-site-id` değeri, `seed.sql` ile eklenen test sitesinin API anahtarıdır.  
Sayfayı aç → `POST http://localhost:5000/api/collect` isteğini tarayıcı DevTools'ta gözlemleyebilirsin.

---

## ⚙️ Konfigürasyon Referansı

### `.env` (Docker Compose)

| Değişken | Varsayılan | Açıklama |
|---|---|---|
| `DB_PASSWORD` | `postgres` | PostgreSQL şifresi |
| `JWT_SECRET` | — | **Zorunlu.** JWT imzalama anahtarı (min. 32 karakter) |
| `DASHBOARD_ORIGIN` | `http://localhost` | Dashboard URL'i — API, yalnızca bu origin'den gelen istekleri kabul eder |
| `TRACKER_URL` | `https://yourdomain.com/tracker.js` | Dashboard'daki embed kodu kopyalama butonu için |

### `api/appsettings.json`

Bu dosya geliştirme ortamı için varsayılan değerleri içerir.  
Docker'da çalışırken tüm değerler `.env` üzerinden otomatik olarak override edilir — bu dosyayı production için düzenlemen gerekmez.

| Alan | Açıklama |
|---|---|
| `Jwt:AccessTokenExpiryMinutes` | Access token ömrü (varsayılan: 15 dk) |
| `Jwt:RefreshTokenExpiryDays` | Refresh token ömrü (varsayılan: 30 gün) |
| `Cors:AllowedOrigins` | Geliştirmede izin verilen origin'ler (Docker'da `.env`'den gelir) |

---

## 📁 Proje Yapısı

```
TrackerForSites/
│
├── docker-compose.yml          # Tüm servisleri tek komutla başlatır
├── .env.example                # Ortam değişkenleri şablonu → .env olarak kopyala
├── .gitignore
│
├── db/
│   ├── schema.sql              # Tablo tanımları, index'ler, trigger'lar
│   │                           # (Docker'da ilk açılışta otomatik çalışır)
│   └── seed.sql                # Geliştirme test verisi — production'da kullanma!
│
├── api/                        # .NET 8 WebAPI
│   ├── Controllers/
│   │   ├── AuthController.cs   # Giriş / Token yenileme / Çıkış
│   │   ├── CollectController.cs # tracker.js'den gelen event alımı
│   │   ├── SitesController.cs  # Site yönetimi (CRUD)
│   │   └── StatsController.cs  # İstatistik endpoint'leri
│   ├── Services/
│   │   ├── FingerprintService.cs  # Cookie'siz unique visitor tanımlama
│   │   ├── GeoIpService.cs        # ip-api.com ile coğrafi konum
│   │   ├── JwtService.cs          # Access / Refresh token üretimi
│   │   ├── StatsAggregatorService.cs # Gece 00:05 UTC çalışan özet servisi
│   │   └── UserAgentService.cs    # Cihaz / tarayıcı tespiti
│   ├── Data/AppDbContext.cs     # EF Core veritabanı bağlamı
│   ├── Models/
│   │   ├── Entities/           # Veritabanı entity'leri
│   │   └── Dtos/               # İstek / Yanıt modelleri
│   └── appsettings.json
│
└── dashboard/                  # React + Vite
    ├── public/
    │   └── tracker.js          # Nginx tarafından /tracker.js olarak serve edilir
    ├── src/
    │   ├── api/client.js       # Tüm API çağrıları — JWT yönetimi ve token yenileme
    │   ├── context/
    │   │   ├── AuthContext.jsx  # Oturum durumu ve JWT parse
    │   │   └── ThemeContext.jsx # Dark/Light mod
    │   ├── components/
    │   │   ├── Sidebar.jsx      # Navigasyon + site seçici
    │   │   ├── StatCard.jsx     # Özet metrik kartı
    │   │   ├── TrafficChart.jsx # Günlük trafik grafiği
    │   │   └── DeviceChart.jsx  # Cihaz dağılımı pasta grafiği
    │   └── pages/
    │       ├── Login.jsx        # Giriş sayfası
    │       ├── Dashboard.jsx    # Ana analitik sayfası
    │       └── Sites.jsx        # Site yönetimi + embed kodu
    ├── nginx.conf
    └── vite.config.js
```

---

## 🔒 Güvenlik

### IP Gizliliği
Ham IP adresi veritabanına **asla** yazılmaz. Fingerprint hesaplandıktan hemen sonra bellekten silinir; yalnızca `SHA256(ip + user-agent + ...)` hash'i saklanır — bu değer geri döndürülemez.

> GDPR ve KVKK kapsamında IP adresi kişisel veri sayılır. Bu yaklaşım her iki mevzuata da uygundur.

### JWT
- **Access token** 15 dakikada bir yenilenir
- **Refresh token** her kullanımda döndürülür (rotation), eski token geçersiz kalır
- Refresh token veritabanında hash olarak saklanır

### BCrypt
Şifreler `cost=12` ile hash'lenir. Geçersiz girişlerde de BCrypt çalıştırılır — timing attack önlemi.

### CORS
- `/api/collect` → tüm origin'lere açık (tracker.js her siteden çağırabilmeli)
- Diğer endpoint'ler → yalnızca `DASHBOARD_ORIGIN`'e izin verilir

### Production Kontrol Listesi

- [ ] `.env` dosyasındaki `JWT_SECRET` uzun ve rastgele mi? (`openssl rand -base64 48`)
- [ ] `db/seed.sql` production'da çalıştırılmadı mı?
- [ ] HTTPS aktif mi? (Reverse proxy veya Nginx SSL)
- [ ] Sunucudaki 5000 portu dışarıya kapalı mı? (Yalnızca 80/443 açık olmalı)

---

## 📄 Lisans

MIT
