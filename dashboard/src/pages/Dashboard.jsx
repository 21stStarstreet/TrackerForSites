import { useState, useEffect, useCallback } from 'react';
import { Users, Eye, TrendingUp, Activity, RefreshCw } from 'lucide-react';
import { api } from '../api/client';
import StatCard from '../components/StatCard';
import TrafficChart from '../components/TrafficChart';
import DeviceChart from '../components/DeviceChart';
import './Dashboard.css';

const DAYS_OPTIONS = [7, 14, 30, 90];

export default function Dashboard({ site }) {
  const [stats, setStats]         = useState(null);
  const [realtime, setRealtime]   = useState(0);
  const [days, setDays]           = useState(30);
  const [loading, setLoading]     = useState(true);
  const [error, setError]         = useState('');

  const load = useCallback(async () => {
    if (!site?.id) return;
    setLoading(true);
    setError('');
    try {
      const data = await api.getStats(site.id, days);
      setStats(data);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, [site?.id, days]);

  // Gerçek zamanlı sayac — her 30 saniyede bir güncellenir
  useEffect(() => {
    if (!site?.id) return;
    const tick = async () => {
      const rt = await api.getRealtime(site.id);
      setRealtime(rt.active_visitors ?? 0);
    };
    tick();
    const interval = setInterval(tick, 30_000);
    return () => clearInterval(interval);
  }, [site?.id]);

  useEffect(() => { load(); }, [load]);

  if (!site) {
    return (
      <div className="dashboard-empty">
        <span style={{ fontSize: 48 }}>🌐</span>
        <h2>Site seç</h2>
        <p>Sol menüden bir site seç veya <strong>Siteler</strong> sayfasında yeni site ekle.</p>
      </div>
    );
  }

  const fmt = n => n >= 1_000_000
    ? `${(n/1_000_000).toFixed(1)}M`
    : n >= 1000 ? `${(n/1000).toFixed(1)}k` : String(n ?? 0);

  return (
    <div className="dashboard animate-in">
      {/* ── Başlık ───────────────────────────────────────────────── */}
      <div className="dashboard-header">
        <div>
          <h1 className="page-title">{site.name}</h1>
          <p className="page-sub">{site.domain}</p>
        </div>

        <div className="dashboard-controls">
          {/* Dönem seçici */}
          <div className="days-selector">
            {DAYS_OPTIONS.map(d => (
              <button
                key={d}
                className={`days-btn ${days === d ? 'days-btn--active' : ''}`}
                onClick={() => setDays(d)}
              >
                {d}g
              </button>
            ))}
          </div>
          <button className="btn btn-ghost" onClick={load} disabled={loading}>
            <RefreshCw size={14} className={loading ? 'spin' : ''} />
          </button>
        </div>
      </div>

      {error && (
        <div className="dashboard-error">{error}</div>
      )}

      {/* ── Özet Kartlar ─────────────────────────────────────────── */}
      <div className="stats-grid">
        <StatCard
          icon={Eye}
          label="Sayfa Görüntüleme"
          value={fmt(stats?.summary?.total_pageviews)}
          sub={`Son ${days} gün`}
          color="accent"
          loading={loading}
        />
        <StatCard
          icon={Users}
          label="Tekil Ziyaretçi"
          value={fmt(stats?.summary?.total_unique_visitors)}
          sub="Fingerprint bazlı"
          color="success"
          loading={loading}
        />
        <StatCard
          icon={TrendingUp}
          label="Bounce Rate"
          value={stats?.summary?.avg_bounce_rate != null
            ? `${(stats.summary.avg_bounce_rate * 100).toFixed(1)}%`
            : '—'}
          sub="Ort. tek sayfa oturumu"
          color="warning"
          loading={loading}
        />
        <StatCard
          icon={Activity}
          label="Şu An Aktif"
          value={realtime}
          sub={<><span className="live-dot" style={{ marginRight: 6 }} />Son 5 dakika</>}
          color="purple"
          loading={false}
        />
      </div>

      {/* ── Grafik ───────────────────────────────────────────────── */}
      <div className="card">
        <div className="card-header">
          <h3 className="card-title">Trafik Grafiği</h3>
          <div className="legend">
            <span className="legend-item legend-item--pv">Görüntüleme</span>
            <span className="legend-item legend-item--uv">Ziyaretçi</span>
          </div>
        </div>
        <TrafficChart data={stats?.daily} loading={loading} />
      </div>

      {/* ── Alt Satır: Sayfalar + Cihazlar ───────────────────────── */}
      <div className="bottom-grid">
        {/* Top Sayfalar */}
        <div className="card">
          <h3 className="card-title" style={{ marginBottom: '1rem' }}>En Çok Ziyaret Edilen</h3>
          {loading ? (
            Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="skeleton" style={{ height: 36, marginBottom: 8 }} />
            ))
          ) : stats?.top_pages?.length ? (
            <table className="table">
              <thead>
                <tr>
                  <th>Sayfa</th>
                  <th style={{ textAlign: 'right' }}>Görüntüleme</th>
                </tr>
              </thead>
              <tbody>
                {stats.top_pages.map((p, i) => (
                  <tr key={i}>
                    <td>
                      <span className="page-rank">{i + 1}</span>
                      <span className="page-url">{p.url}</span>
                    </td>
                    <td style={{ textAlign: 'right', fontWeight: 600 }}>
                      {p.views.toLocaleString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <div style={{ color: 'var(--text-muted)', fontSize: 14, textAlign: 'center', padding: '2rem' }}>
              Veri yok
            </div>
          )}
        </div>

        {/* Cihaz Dağılımı */}
        <div className="card">
          <h3 className="card-title" style={{ marginBottom: '1rem' }}>Cihaz Dağılımı</h3>
          <DeviceChart devices={stats?.devices} loading={loading} />
        </div>

        {/* Top Referrerlar */}
        <div className="card">
          <h3 className="card-title" style={{ marginBottom: '1rem' }}>Trafik Kaynakları</h3>
          {loading ? (
            Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="skeleton" style={{ height: 36, marginBottom: 8 }} />
            ))
          ) : stats?.top_referrers?.length ? (
            <table className="table">
              <thead>
                <tr>
                  <th>Kaynak</th>
                  <th style={{ textAlign: 'right' }}>Ziyaret</th>
                </tr>
              </thead>
              <tbody>
                {stats.top_referrers.map((r, i) => (
                  <tr key={i}>
                    <td>{r.domain ?? 'Direkt'}</td>
                    <td style={{ textAlign: 'right', fontWeight: 600 }}>
                      {r.count.toLocaleString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <div style={{ color: 'var(--text-muted)', fontSize: 14, textAlign: 'center', padding: '2rem' }}>
              Veri yok
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
