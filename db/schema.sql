-- TABLO HİYERARŞİSİ:
--   users          -> dashboard kullanıcıları (kimlik doğrulama)
--   refresh_tokens -> JWT refresh token'ları (logout + revoke)
--   sites          -> kayıtlı siteler (ana varlık)
--   events         -> ham event verisi (her pageview = 1 satır)
--   daily_stats    -> günlük özet (dashboard için hızlı okuma)
--
-- ÇALIŞTIRMAK İÇİN:
--   psql -U postgres -d trackerdb -f schema.sql
-- ==================================================================


-- ─────────────────────────────────────────────────────────────────
-- UZANTI: pgcrypto
-- gen_random_uuid() için gerekli. Tutarlılık için UUID üretimini
-- PostgreSQL yapar, uygulama katmanına bırakmıyoruz .
-- ─────────────────────────────────────────────────────────────────
CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- ─────────────────────────────────────────────────────────────────
-- TABLO 1: users
-- Dashboard'a giriş yapan kullanıcılar.
-- bcrypt hash'i saklanır.
-- ─────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS users (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    email           TEXT NOT NULL UNIQUE,
    password_hash   TEXT NOT NULL,

    -- Kullanıcının görünen adı
    full_name       TEXT,

    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_users_email ON users USING HASH (email);


-- ─────────────────────────────────────────────────────────────────
-- TABLO 2: refresh_tokens
-- JWT refresh token'larını saklar.
--
-- Neden DB'de saklıyoruz?
-- JWT stateless'tır — sunucu token'ı "unutur".
-- Kullanıcı logout olduğunda access_token geçersiz kılınamaz
-- (süresi dolana kadar). Refresh token DB'de olursa:
--   - logout → token silinir → yeni access_token üretilemez
--   - şifre değişikliği → tüm token'lar silinir
--   - "tüm cihazlardan çıkış" özelliği mümkün olur
-- ─────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS refresh_tokens (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,

    -- Token'ın kendisini değil, SHA256 hash'ini saklıyoruz.
    -- Neden? DB ele geçirilse bile token'lar kullanılamaz.
    token_hash  CHAR(64) NOT NULL UNIQUE,

    -- Token ne zaman geçersiz olur?
    expires_at  TIMESTAMPTZ NOT NULL,

    -- Hangi cihaz/tarayıcıdan oluşturuldu? (opsiyonel, UX için)
    user_agent  TEXT,

    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- null = aktif, dolu = iptal edildi (logout veya şifre değişikliği)
    revoked_at  TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user
    ON refresh_tokens (user_id);
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_hash
    ON refresh_tokens USING HASH (token_hash);

-- Süresi dolmuş token temizleme sorgusu için:
-- DELETE FROM refresh_tokens WHERE expires_at < NOW()
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_expires
    ON refresh_tokens (expires_at);


-- ─────────────────────────────────────────────────────────────────
-- TABLO 3: sites
-- Sisteme kayıtlı her web sitesi bir satır.
-- ─────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS sites (

    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Hangi kullanıcıya ait?
    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,

    -- Site adı (kullanıcının verdiği, örn: "Nutreon Blog")
    name        TEXT NOT NULL,

    -- Domain
    -- sadece bir kez kaydedilebilir
    domain      TEXT NOT NULL UNIQUE,

    -- API key: tracker.js'in data-site-id değeri bu olacak.
    -- gen_random_uuid() ile üretilir, hash'e gerek yok.
    api_key     TEXT NOT NULL UNIQUE DEFAULT gen_random_uuid()::TEXT,


    is_active   BOOLEAN NOT NULL DEFAULT TRUE,

    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- İndeks: api_key ile çok sık sorgu yapılacak (her event'te doğrulama).
-- Hash index, equality check için B-tree'den daha hızlı.
CREATE INDEX IF NOT EXISTS idx_sites_api_key ON sites USING HASH (api_key);
CREATE INDEX IF NOT EXISTS idx_sites_user_id ON sites (user_id);


-- ─────────────────────────────────────────────────────────────────
-- TABLO 4: events
-- Her pageview, her custom event = 1 satır.
-- ─────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS events (
    -- BIGSERIAL (auto-increment int8) seçtik, UUID değil.
    -- Neden? Bu tablo milyonlarca satıra ulaşacak.
    -- UUID primary key, her insert'te rastgele index pozisyonu
    -- -> sayfa bölünmesi (page split) -> yavaşlama.
    -- BIGSERIAL → sıralı insert → B-tree index sağlıklı büyür.
    id              BIGSERIAL PRIMARY KEY,

    -- Hangi siteye ait? sites tablosuna foreign key.
    -- ON DELETE CASCADE: site silinirse tüm event'leri de sil.
    site_id         UUID NOT NULL REFERENCES sites(id) ON DELETE CASCADE,

    -- Event tipi: "pageview" veya custom event adı
    event_type      TEXT NOT NULL DEFAULT 'pageview',

    -- ── İstemciden gelen alanlar ──────────────────────────────

    -- tracker.js'den gelen URL (pathname + search, örn: /blog?page=2)
    url             TEXT NOT NULL,

    -- Kullanıcı nereden geldi? null olabilir.
    referrer        TEXT,

    -- Referrer'ın sadece domain kısmı. Dashboard "Top Referrers"
    -- gösterirken tam URL değil domain gruplaması yapar.
    -- API insert'te bir kez parse eder.
    referrer_domain TEXT,

    -- Sayfanın <title> değeri
    page_title      TEXT,

    -- navigator.language (örn: "tr-TR", "en-US")
    language        VARCHAR(10),

    -- Ekran genişliği (responsive tasarım analizi için)
    screen_width    SMALLINT,

    -- İstemciden gelen oturum ID (sessionStorage'dan)
    session_id      TEXT NOT NULL,

    -- İstemcinin saati.
    -- NOT: tracker.js, ts alanını Date.now() ile gönderir (Unix ms, integer).
    -- API bu değeri TIMESTAMPTZ'e dönüştürür:
    --   DateTimeOffset.FromUnixTimeMilliseconds(ts)
    client_ts       TIMESTAMPTZ,

    -- ── Sunucunun eklediği alanlar ───────────────────────────

    -- IP adresi SAKLANMAZ. Sadece SHA256 hash'i saklanır.
    -- Neden? GDPR/KVKK: IP kişisel veridir. Hash geri döndürülemez.
    -- Fingerprint hesaplandıktan sonra ham IP hafızadan silinir.
    ip_hash         CHAR(64),           -- SHA256 = 64 hex karakter

    -- User-Agent string (tarayıcı ile işletim sistemi)
    -- Fingerprint için ve cihaz tespiti için saklanır.
    user_agent      TEXT,

    -- Fingerprint: SHA256(ip , user_agent , language ve screen_width)
    -- Bu değer "unique visitor" sayımında kullanılır.
    -- Cookie'siz, privacy-first takip yöntemi.
    fingerprint     CHAR(64) NOT NULL,

    -- UA'dan parse edilen alanlar (insert'te bir kez hesaplanır)
    -- Her sorguda UA parse etmek çok yavaş olur.
    -- API insert sırasında UAParser ile ayrıştırır, saklarız.

    -- Tarayıcı adı
    browser         VARCHAR(50),

    -- İşletim sistemi 
    os              VARCHAR(50),

    -- Cihaz türü
    -- screen_width'ten de türetilebilir ama UA daha güvenilir.
    device_type     VARCHAR(10),

    -- MaxMind GeoIP veya ip-api.com'dan alınacak
    country_code    CHAR(2),            -- ISO 3166-1 (örn: "TR", "US")
    city            TEXT,               -- (örn: "Mersin")

    -- Sunucunun event'i kaydettiği an. Asıl zaman damgası bu.
    -- DEFAULT NOW() her insert'te otomatik set edilir.
    server_ts       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ── İndeksler ────────────────────────────────────────────────────

-- En sık sorgu: "X sitesinin son N günündeki event'leri getir"
-- B-tree seçtik çünkü:
-- 1. (site_id, server_ts) composite, B-tree zorunlu (BRIN composite desteklemez)
-- 2. DESC sıralama, BRIN desteklemez
-- 3. server_ts sıralı insert edildiği için B-tree'nin dezavantajı minimal
CREATE INDEX IF NOT EXISTS idx_events_site_ts
    ON events (site_id, server_ts DESC);

-- Unique visitor sayımı: "Bu sitede kaç farklı fingerprint var?"
CREATE INDEX IF NOT EXISTS idx_events_fingerprint
    ON events (site_id, fingerprint);

-- Session bazlı sorgular (kullanıcı başına sayfa sayısı vb.)
CREATE INDEX IF NOT EXISTS idx_events_session
    ON events (session_id);

-- Sayfa bazlı analiz: "En çok hangi URL ziyaret edildi?"
CREATE INDEX IF NOT EXISTS idx_events_url
    ON events (site_id, url);

-- Cihaz/tarayıcı dağılımı sorguları
CREATE INDEX IF NOT EXISTS idx_events_device
    ON events (site_id, device_type);


-- ─────────────────────────────────────────────────────────────────
-- TABLO 5: daily_stats
-- Her gün, her site için önceden hesaplanmış özet.
-- API her gece (veya her event'ten sonra) bu tabloyu günceller.
-- Dashboard bu tabloyu okur, events'e dokunmaz.
-- ─────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS daily_stats (
    id                  BIGSERIAL PRIMARY KEY,

    site_id             UUID NOT NULL REFERENCES sites(id) ON DELETE CASCADE,

    -- Hangi gün? (zaman dilimi olmadan, UTC gün)
    stat_date           DATE NOT NULL,

    -- Toplam sayfa görüntüleme
    pageviews           INTEGER NOT NULL DEFAULT 0,

    -- Tekil ziyaretçi (unique fingerprint sayısı)
    unique_visitors     INTEGER NOT NULL DEFAULT 0,

    -- Tekil oturum sayısı
    unique_sessions     INTEGER NOT NULL DEFAULT 0,

    -- Bounce rate: sadece 1 sayfa görüp giden oturum yüzdesi
    -- 0.00 - 1.00 arasında (örn: 0.42 = %42)
    bounce_rate         NUMERIC(4,3),

    -- En çok ziyaret edilen sayfalar (JSON array, hızlı okuma için)
    -- Örn: [{"url": "/blog", "views": 142}, {"url": "/", "views": 98}]
    top_pages           JSONB,

    -- En çok trafik gönderen referrer'lar
    -- Örn: [{"ref": "google.com", "count": 87}]
    top_referrers       JSONB,

    -- Ülke dağılımı
    -- Örn: {"TR": 312, "US": 45, "DE": 23}
    country_breakdown   JSONB,

    -- Tarayıcı dağılımı
    -- Örn: {"Chrome": 210, "Safari": 87, "Firefox": 43}
    browser_breakdown   JSONB,

    -- Cihaz dağılımı
    -- Örn: {"desktop": 245, "mobile": 187, "tablet": 23}
    device_breakdown    JSONB,

    -- Güncelleme zamanı (ne zaman hesaplandı?)
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- (site_id, stat_date) çifti unique olmalı -> bir günde bir özet.
CREATE UNIQUE INDEX IF NOT EXISTS idx_daily_stats_site_date
    ON daily_stats (site_id, stat_date);


-- ─────────────────────────────────────────────────────────────────
-- TRIGGER: updated_at otomatik güncelleme
-- users ve sites tablolarında UPDATE yapıldığında
-- updated_at kolonunu otomatik olarak NOW() yapar.
-- Bunu API'ye bırakırsak unutulabilir bu yüzden DB'de garantilemek daha güvenli.
-- ─────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE TRIGGER trg_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE OR REPLACE TRIGGER trg_sites_updated_at
    BEFORE UPDATE ON sites
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();


-- Test verisi için: db/seed.sql dosyasını kullanın.
-- Production'da asla seed.sql çalıştırmayın.
-- Geliştirme ortamı için:
--   psql -U postgres -d trackerdb -f db/seed.sql
