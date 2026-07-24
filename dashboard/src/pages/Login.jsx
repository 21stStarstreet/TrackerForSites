import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import { Zap, Moon, Sun, Eye, EyeOff } from 'lucide-react';
import './Login.css';

export default function Login() {
  const { login } = useAuth();
  const { theme, toggle } = useTheme();
  const navigate = useNavigate();

  const [email, setEmail]       = useState('');
  const [password, setPassword] = useState('');
  const [showPw, setShowPw]     = useState(false);
  const [error, setError]       = useState('');
  const [loading, setLoading]   = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await login(email, password);
      navigate('/');
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      {/* Arka plan dekoratif elementler */}
      <div className="login-bg">
        <div className="login-glow login-glow--1" />
        <div className="login-glow login-glow--2" />
      </div>

      {/* Tema butonu */}
      <button className="theme-toggle-float" onClick={toggle}>
        {theme === 'dark' ? <Sun size={16} /> : <Moon size={16} />}
      </button>

      <div className="login-card card-glass animate-in">
        {/* Logo */}
        <div className="login-logo">
          <div className="logo-icon">
            <Zap size={22} />
          </div>
          <div>
            <h1 className="login-title">TrackerFS</h1>
            <p className="login-subtitle">Analytics Dashboard</p>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="login-form">
          <div className="field">
            <label className="field-label">E-posta</label>
            <input
              type="email"
              className="input"
              placeholder="admin@example.com"
              value={email}
              onChange={e => setEmail(e.target.value)}
              required
              autoFocus
            />
          </div>

          <div className="field">
            <label className="field-label">Şifre</label>
            <div className="pw-wrap">
              <input
                type={showPw ? 'text' : 'password'}
                className="input"
                placeholder="••••••••"
                value={password}
                onChange={e => setPassword(e.target.value)}
                required
              />
              <button
                type="button"
                className="pw-toggle"
                onClick={() => setShowPw(s => !s)}
                tabIndex={-1}
              >
                {showPw ? <EyeOff size={15} /> : <Eye size={15} />}
              </button>
            </div>
          </div>

          {error && <div className="login-error">{error}</div>}

          <button type="submit" className="btn btn-primary login-btn" disabled={loading}>
            {loading ? (
              <span className="btn-spinner" />
            ) : 'Giriş Yap'}
          </button>
        </form>
      </div>
    </div>
  );
}
