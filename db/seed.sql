-- ─────────────────────────────────────────────────────────────────
-- SEED VERİSİ — Sadece geliştirme ortamı için!
-- PRODUCTION'DA ASLA ÇALIŞTIRMAYINIZ.
--
-- Çalıştırmak için:
--   psql -U postgres -d trackerdb -f db/seed.sql
--
-- Test hesabı:
--   Email:    admin@trackerforsite.com
--   Şifre:    password123
-- ─────────────────────────────────────────────────────────────────

INSERT INTO users (email, password_hash, full_name)
VALUES (
    'admin@trackerforsite.com',
    '$2a$12$/U5M/vqp5Dxtspcr4xJFoO9WetKnbLint2TyT4KAjYx7Dv9RCU8Vi',
    'Admin'
)
ON CONFLICT (email) DO NOTHING;

INSERT INTO sites (name, domain, api_key, user_id)
SELECT 'Test Sitesi', 'localhost', 'test-key-local', id
FROM users WHERE email = 'admin@trackerforsite.com'
ON CONFLICT (domain) DO NOTHING;
