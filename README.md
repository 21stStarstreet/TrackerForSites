# ⚡ TrackerForSites

<p align="center">
  <img src="https://img.shields.io/badge/C%23-12.0-239120?logo=csharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 8" />
  <img src="https://img.shields.io/badge/Entity_Framework-Core-512BD4?logo=dotnet&logoColor=white" alt="Entity Framework" />
  <img src="https://img.shields.io/badge/JavaScript-ES6+-F7DF1E?logo=javascript&logoColor=black" alt="JavaScript" />
  <img src="https://img.shields.io/badge/HTML5-Semantic-E34F26?logo=html5&logoColor=white" alt="HTML5" />
  <img src="https://img.shields.io/badge/CSS3-Custom_Design-1572B6?logo=css3&logoColor=white" alt="CSS3" />
  <img src="https://img.shields.io/badge/React-18.0-61DAFB?logo=react&logoColor=black" alt="React" />
  <img src="https://img.shields.io/badge/Vite-Lightning_Fast-646CFF?logo=vite&logoColor=white" alt="Vite" />
  <img src="https://img.shields.io/badge/PostgreSQL-Relational_DB-4169E1?logo=postgresql&logoColor=white" alt="PostgreSQL" />
  <img src="https://img.shields.io/badge/JWT-Auth-000000?logo=jsonwebtokens&logoColor=white" alt="JWT" />
  <img src="https://img.shields.io/badge/BCrypt-Hashing-4A4A4A?logo=letsencrypt&logoColor=white" alt="BCrypt" />
  <img src="https://img.shields.io/badge/Docker-Container-2496ED?logo=docker&logoColor=white" alt="Docker" />
  <img src="https://img.shields.io/badge/Nginx-Proxy-009639?logo=nginx&logoColor=white" alt="Nginx" />
</p>

**TrackerForSites**, Google Analytics gibi platformlara karşı geliştirilmiş hafif ve modern bir alternatiftir. 

**Projenin Amacı:** Web sitelerinin hızını düşürmeden (<1 KB script), **çerez (cookie) kullanmadan** ve ziyaretçilerin **gizliliğini ihlal etmeden** en önemli istatistikleri (anlık ziyaretçi, sayfa görüntülenmesi, cihaz/tarayıcı dağılımı vb.) doğrudan site sahibine sunmaktır. Verileriniz tamamen kendi sunucunuzda (self-hosted) kalır, asla üçüncü taraflara satılmaz veya paylaşılmaz.

---

## 💡 Nasıl Çalışır?

Docker yüklü herhangi bir sunucuya kopyala, `.env` ayarlarını yap ve başlat, hepsi bu.  
Veritabanı, API ve arayüz tek bir komutla birlikte ayağa kalkar.

Çalışmaya başlayınca izlemek istediğin sitenin `<head>` bölümüne tek satır `<script>` eklersin, veriler anında akmaya başlar.

---

## 📋 İçindekiler

- [Özellikler](#ozellikler)
- [Mimari](#mimari)
- [Başlangıç — Production (Docker)](#baslangic)
- [Geliştirme Ortamı](#gelistirme)
- [Konfigürasyon Referansı](#konfigurasyon)
- [Proje Yapısı](#proje-yapisi)
- [Güvenlik](#guvenlik)

---

<a name="ozellikler"></a>
## ✨ Özellikler

<!-- EKRAN GÖRÜNTÜSÜ BURAYA: ![Dashboard](DOSYA_ADI.png) -->

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

<a name="mimari"></a>
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
- API yalnızca `api_key` doğrular ve olayı arka plan kuyruğuna atar (~12ms)
- `EventQueueService` arka planda fingerprint, GeoIP ve UA parse yaparak 50'li batch'ler halinde veritabanına yazar
- Her gece 00:05 UTC'de `StatsAggregatorService` günlük özetleri hesaplar
- Dashboard, JWT ile oturum açarak istatistikleri çeker
- "Şu An Aktif" sayacı SSE (Server-Sent Events) ile her 10 saniyede push güncelleme alır

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

<a name="baslangic"></a>
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

`.env` dosyasını aç. **Değiştirilmesi gereken 2 yer var:**

| Değişken | Ne yazacaksın |
|---|---|
| `JWT_SECRET` | Rastgele, uzun bir metin — örnek: `openssl rand -base64 48` çıktısı |
| `DASHBOARD_ORIGIN` | Dashboard'unun yayınlandığı URL — örn: `https://analytics.siten.com` |

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

<a name="gelistirme"></a>
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

> [!NOTE]
> **Embed Kodundaki URL Hakkında**
> Dashboard'da size verilen embed kodunda `src` adresi otomatik olarak bulunduğunuz sunucuya göre ayarlanır (örn: local'deyseniz `http://localhost/tracker.js`, prodüksiyondaysanız `https://analytics.siten.com/tracker.js`). Local geliştirme ortamında test yaparken bu kodu direkt canlı sitenize eklerseniz çalışmaz (ziyaretçinin kendi `localhost`'unu arayacağı için).

Local test için kendi bilgisayarınızda basit bir HTML dosyası (`test.html`) oluşturup tarayıcıda `http://localhost/test.html` (veya `localhost:3000`) üzerinden açarak test edebilirsiniz:

```html
<script async defer
  src="http://localhost:3000/tracker.js"
  data-site-id="test-key-local">
</script>
```

`data-site-id` değeri, `seed.sql` ile eklenen test sitesinin API anahtarıdır.  
Sayfayı aç → `POST http://localhost:5000/api/collect` isteğini tarayıcı DevTools'ta gözlemleyebilirsin.

---

<a name="konfigurasyon"></a>
## ⚙️ Konfigürasyon Referansı

### `.env` (Docker Compose)

| Değişken | Varsayılan | Açıklama |
|---|---|---|
| `DB_PASSWORD` | `postgres` | PostgreSQL şifresi |
| `JWT_SECRET` | — | **Zorunlu.** JWT imzalama anahtarı (min. 32 karakter) |
| `DASHBOARD_ORIGIN` | `http://localhost` | Dashboard URL'i — API, yalnızca bu origin'den gelen istekleri kabul eder |

### `api/appsettings.json`

Bu dosya geliştirme ortamı için varsayılan değerleri içerir.  
Docker'da çalışırken tüm değerler `.env` üzerinden otomatik olarak override edilir — bu dosyayı production için düzenlemen gerekmez.

| Alan | Açıklama |
|---|---|
| `Jwt:AccessTokenExpiryMinutes` | Access token ömrü (varsayılan: 15 dk) |
| `Jwt:RefreshTokenExpiryDays` | Refresh token ömrü (varsayılan: 30 gün) |
| `Cors:AllowedOrigins` | Geliştirmede izin verilen origin'ler (Docker'da `.env`'den gelir) |

---

<a name="proje-yapisi"></a>
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

<a name="guvenlik"></a>
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

### Rate Limiting
- `POST /api/collect` — 60 istek/dakika/IP (kayar pencere)
- `POST /api/auth/*` — 10 istek/dakika/IP (brute-force koruması)
- Aşımında `429 Too Many Requests` + JSON hata mesajı

### CORS
- `/api/collect` → tüm origin'lere açık (tracker.js her siteden çağırabilmeli)
- Diğer endpoint'ler → yalnızca `DASHBOARD_ORIGIN`'e izin verilir

### Production Kontrol Listesi

- [ ] `.env` dosyasındaki `JWT_SECRET` uzun ve rastgele mi? (`openssl rand -base64 48`)
- [ ] `db/seed.sql` production'da çalıştırılmadı mı?
- [ ] HTTPS aktif mi? (Reverse proxy veya Nginx SSL)
- [ ] Sunucudaki 5000 portu dışarıya kapalı mı? (Yalnızca 80/443 açık olmalı)
