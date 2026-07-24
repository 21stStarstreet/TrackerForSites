import { useState, useEffect } from 'react';
import { Plus, Trash2, Copy, Check, Globe, Code } from 'lucide-react';
import { api } from '../api/client';
import './Sites.css';

export default function Sites({ onSiteChange }) {
  const [sites, setSites]     = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState('');
  const [showForm, setShowForm] = useState(false);
  const [name, setName]       = useState('');
  const [domain, setDomain]   = useState('');
  const [creating, setCreating] = useState(false);
  const [copied, setCopied]   = useState(null); // hangi site'ın kodu kopyalandı
  const [newSite, setNewSite] = useState(null); // yeni oluşturulan site (embed kodu için)

  const load = async () => {
    setLoading(true);
    try {
      const data = await api.getSites();
      setSites(data);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const handleCreate = async (e) => {
    e.preventDefault();
    setCreating(true);
    setError('');
    try {
      const created = await api.createSite(name, domain);
      setNewSite(created);
      setName('');
      setDomain('');
      setShowForm(false);
      await load();
      onSiteChange?.();
    } catch (e) {
      setError(e.message);
    } finally {
      setCreating(false);
    }
  };

  const handleDelete = async (id, siteName) => {
    if (!confirm(`"${siteName}" sitesini silmek istiyor musun?\nGeçmiş veriler korunur.`)) return;
    try {
      await api.deleteSite(id);
      setSites(s => s.filter(x => x.id !== id));
      onSiteChange?.();
    } catch (e) {
      setError(e.message);
    }
  };

  const copyEmbed = (site) => {
    // VITE_TRACKER_URL .env'de ayarlanmazsa "yourdomain.com" placeholder kullanılır
    const trackerUrl = import.meta.env.VITE_TRACKER_URL || 'https://yourdomain.com/tracker.js';
    const code = `<script async defer src="${trackerUrl}" data-site-id="${site.apiKey}"><\/script>`;
    navigator.clipboard.writeText(code);
    setCopied(site.id);
    setTimeout(() => setCopied(null), 2000);
  };

  const embedCode = (apiKey) =>
    `<script async defer\n  src="https://yourdomain.com/tracker.js"\n  data-site-id="${apiKey}">\n<\/script>`;

  return (
    <div className="sites-page animate-in">
      <div className="sites-header">
        <div>
          <h1 className="page-title">Siteler</h1>
          <p className="page-sub">Tracker embed kodunu kopyalayarak sitenize ekleyin.</p>
        </div>
        <button className="btn btn-primary" onClick={() => setShowForm(s => !s)}>
          <Plus size={16} />
          Yeni Site
        </button>
      </div>

      {error && <div className="site-error">{error}</div>}

      {/* ── Yeni site formu ───────────────────────────────────────── */}
      {showForm && (
        <div className="card animate-in" style={{ maxWidth: 480 }}>
          <h3 className="card-title" style={{ marginBottom: '1.25rem' }}>Site Ekle</h3>
          <form onSubmit={handleCreate} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <div>
              <label className="field-label">Site Adı</label>
              <input
                className="input"
                placeholder="Örn: Benim Blogum"
                value={name}
                onChange={e => setName(e.target.value)}
                required
                autoFocus
              />
            </div>
            <div>
              <label className="field-label">Domain</label>
              <input
                className="input"
                placeholder="orneksite.com"
                value={domain}
                onChange={e => setDomain(e.target.value)}
                required
              />
            </div>
            <div style={{ display: 'flex', gap: '0.75rem' }}>
              <button type="submit" className="btn btn-primary" disabled={creating}>
                {creating ? <span className="btn-spinner" /> : 'Ekle'}
              </button>
              <button type="button" className="btn btn-ghost" onClick={() => setShowForm(false)}>
                İptal
              </button>
            </div>
          </form>
        </div>
      )}

      {/* ── Yeni site embed kodu ──────────────────────────────────── */}
      {newSite && (
        <div className="embed-success animate-in">
          <div className="embed-success-header">
            <Check size={18} className="embed-check" />
            <strong>"{newSite.name}" eklendi!</strong>
          </div>
          <p className="embed-hint">Bu kodu sitenizin &lt;head&gt; etiketine ekleyin:</p>
          <pre className="embed-code">{embedCode(newSite.apiKey)}</pre>
          <button className="btn btn-ghost" style={{ fontSize: 12 }} onClick={() => setNewSite(null)}>
            Kapat
          </button>
        </div>
      )}

      {/* ── Site listesi ─────────────────────────────────────────── */}
      {loading ? (
        <div className="sites-grid">
          {[1,2,3].map(i => (
            <div key={i} className="card skeleton" style={{ height: 160 }} />
          ))}
        </div>
      ) : sites.length === 0 ? (
        <div className="sites-empty">
          <Globe size={40} style={{ color: 'var(--text-muted)' }} />
          <h3>Henüz site yok</h3>
          <p>Yukarıdaki "Yeni Site" butonuna basarak başlayın.</p>
        </div>
      ) : (
        <div className="sites-grid">
          {sites.map(site => (
            <div key={site.id} className="site-card card">
              <div className="site-card-header">
                <div className="site-globe"><Globe size={16} /></div>
                <div style={{ flex: 1, overflow: 'hidden' }}>
                  <div className="site-name">{site.name}</div>
                  <div className="site-domain">{site.domain}</div>
                </div>
                <button
                  className="btn btn-danger"
                  style={{ padding: '0.35rem 0.6rem' }}
                  onClick={() => handleDelete(site.id, site.name)}
                  title="Sil"
                >
                  <Trash2 size={14} />
                </button>
              </div>

              <div className="site-api-key">
                <Code size={12} />
                <code>{site.apiKey}</code>
              </div>

              <button
                className={`btn ${copied === site.id ? 'btn-primary' : 'btn-ghost'} site-copy-btn`}
                onClick={() => copyEmbed(site)}
              >
                {copied === site.id ? (
                  <><Check size={14} /> Kopyalandı!</>
                ) : (
                  <><Copy size={14} /> Embed Kodu</>
                )}
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
