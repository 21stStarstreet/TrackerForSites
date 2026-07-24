-- Seed verisini çalıştırarak kullanıcıyı ve siteyi oluştur


-- Sahte trafik verisi üretimi
DO $$
DECLARE
    v_site_id UUID;
    v_date DATE;
    v_pageviews INT;
    v_visitors INT;
    v_sessions INT;
    v_bounce_rate NUMERIC;
    v_top_pages JSONB;
    v_top_referrers JSONB;
    v_country JSONB;
    v_browser JSONB;
    v_device JSONB;
BEGIN
    -- Test Sitesi'nin ID'sini al
    SELECT id INTO v_site_id FROM sites WHERE domain = 'localhost' LIMIT 1;
    
    IF v_site_id IS NULL THEN
        RAISE NOTICE 'Site bulunamadı, çıkılıyor.';
        RETURN;
    END IF;

    -- Son 7 gün için rastgele ama gerçekçi veriler oluştur
    FOR i IN 0..6 LOOP
        v_date := CURRENT_DATE - i;
        
        -- Gerçekçi bir trend oluşturmak için (hafta sonu düşüşü vb. simülesi)
        v_pageviews := floor(random() * (2000 - 500 + 1) + 500);
        v_visitors := floor(v_pageviews * (random() * (0.8 - 0.4) + 0.4));
        v_sessions := floor(v_visitors * 1.1);
        v_bounce_rate := random() * (0.6 - 0.2) + 0.2;
        
        v_top_pages := ('[
            {"url": "/", "views": ' || (v_pageviews * 0.4)::int || '},
            {"url": "/about", "views": ' || (v_pageviews * 0.2)::int || '},
            {"url": "/pricing", "views": ' || (v_pageviews * 0.15)::int || '},
            {"url": "/blog", "views": ' || (v_pageviews * 0.1)::int || '},
            {"url": "/contact", "views": ' || (v_pageviews * 0.05)::int || '}
        ]')::jsonb;
        
        v_top_referrers := ('[
            {"domain": "google.com", "count": ' || (v_sessions * 0.5)::int || '},
            {"domain": "twitter.com", "count": ' || (v_sessions * 0.2)::int || '},
            {"domain": "github.com", "count": ' || (v_sessions * 0.1)::int || '},
            {"domain": "direct", "count": ' || (v_sessions * 0.1)::int || '}
        ]')::jsonb;
        
        v_country := ('{"TR": ' || (v_visitors * 0.6)::int || ', "US": ' || (v_visitors * 0.2)::int || ', "DE": ' || (v_visitors * 0.1)::int || ', "GB": ' || (v_visitors * 0.05)::int || '}')::jsonb;
        v_browser := ('{"Chrome": ' || (v_visitors * 0.6)::int || ', "Safari": ' || (v_visitors * 0.25)::int || ', "Firefox": ' || (v_visitors * 0.1)::int || '}')::jsonb;
        v_device := ('{"desktop": ' || (v_visitors * 0.55)::int || ', "mobile": ' || (v_visitors * 0.4)::int || ', "tablet": ' || (v_visitors * 0.05)::int || '}')::jsonb;


        INSERT INTO daily_stats (
            site_id, stat_date, pageviews, unique_visitors, unique_sessions, 
            bounce_rate, top_pages, top_referrers, country_breakdown, browser_breakdown, device_breakdown
        ) VALUES (
            v_site_id, v_date, v_pageviews, v_visitors, v_sessions,
            v_bounce_rate, v_top_pages, v_top_referrers, v_country, v_browser, v_device
        ) ON CONFLICT (site_id, stat_date) DO UPDATE SET
            pageviews = EXCLUDED.pageviews,
            unique_visitors = EXCLUDED.unique_visitors,
            unique_sessions = EXCLUDED.unique_sessions,
            bounce_rate = EXCLUDED.bounce_rate,
            top_pages = EXCLUDED.top_pages,
            top_referrers = EXCLUDED.top_referrers,
            country_breakdown = EXCLUDED.country_breakdown,
            browser_breakdown = EXCLUDED.browser_breakdown,
            device_breakdown = EXCLUDED.device_breakdown;
    END LOOP;
END $$;
