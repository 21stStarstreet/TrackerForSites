import React from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { LayoutDashboard, Globe, LogOut, Sun, Moon, ChevronDown, Zap } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import './Sidebar.css';

const NAV = [
  { to: '/',      icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/sites', icon: Globe,           label: 'Siteler'   },
];

export default function Sidebar({ sites = [], activeSite, onSiteChange }) {
  const { user, logout } = useAuth();
  const { theme, toggle } = useTheme();
  const navigate = useNavigate();
  const [siteOpen, setSiteOpen] = React.useState(false);
  const selectorRef = React.useRef(null);

  // Dışarı tıklayınca veya Escape'e basınca dropdown'ı kapat
  React.useEffect(() => {
    if (!siteOpen) return;
    const handleClick = (e) => {
      if (selectorRef.current && !selectorRef.current.contains(e.target)) {
        setSiteOpen(false);
      }
    };
    const handleKey = (e) => { if (e.key === 'Escape') setSiteOpen(false); };
    document.addEventListener('mousedown', handleClick);
    document.addEventListener('keydown', handleKey);
    return () => {
      document.removeEventListener('mousedown', handleClick);
      document.removeEventListener('keydown', handleKey);
    };
  }, [siteOpen]);

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  return (
    <aside className="sidebar">
      <div className="sidebar-logo">
        <div className="logo-icon" style={{ padding: 0, overflow: 'hidden', background: 'transparent' }}>
          <img src="/logo.jpg" alt="TrackerFS Logo" style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
        </div>
        <span className="logo-text">TrackerFS</span>
      </div>

      {/* Site Seçici */}
      {sites.length > 0 && (
        <div className="site-selector" ref={selectorRef}>
          <button
            className="site-selector-btn"
            onClick={() => setSiteOpen(o => !o)}
          >
            <Globe size={14} />
            <span className="site-selector-name">
              {activeSite?.name ?? 'Site seç'}
            </span>
            <ChevronDown size={14} className={siteOpen ? 'chevron-open' : ''} />
          </button>
          {siteOpen && (
            <div className="site-dropdown">
              {sites.map(s => (
                <button
                  key={s.id}
                  className={`site-option ${activeSite?.id === s.id ? 'site-option--active' : ''}`}
                  onClick={() => { onSiteChange(s); setSiteOpen(false); navigate('/'); }}
                >
                  <span>{s.name}</span>
                  {activeSite?.id === s.id && <span className="site-check">✓</span>}
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Navigation */}
      <nav className="sidebar-nav">
        {NAV.map(({ to, icon: Icon, label }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            className={({ isActive }) =>
              `nav-item ${isActive ? 'nav-item--active' : ''}`
            }
          >
            <Icon size={18} />
            <span>{label}</span>
          </NavLink>
        ))}
      </nav>

      {/* Footer */}
      <div className="sidebar-footer">
        <div className="user-info">
          <div className="user-avatar">
            {user?.name?.[0]?.toUpperCase() ?? 'U'}
          </div>
          <div className="user-details">
            <div className="user-name">{user?.name ?? 'Kullanıcı'}</div>
            <div className="user-email">{user?.email}</div>
          </div>
        </div>

        <div className="sidebar-actions">
          <button className="action-btn" onClick={toggle} title="Tema değiştir">
            {theme === 'dark' ? <Sun size={16} /> : <Moon size={16} />}
          </button>
          <button className="action-btn action-btn--danger" onClick={handleLogout} title="Çıkış">
            <LogOut size={16} />
          </button>
        </div>
      </div>
    </aside>
  );
}
