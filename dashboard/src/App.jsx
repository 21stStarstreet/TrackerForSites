import { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './context/AuthContext';
import { ThemeProvider } from './context/ThemeContext';
import Sidebar from './components/Sidebar';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import Sites from './pages/Sites';
import { api } from './api/client';

// Auth gerektiren sayfalar için wrapper
function ProtectedLayout() {
  const { isAuth } = useAuth();
  const [sites, setSites]           = useState([]);
  const [activeSite, setActiveSite] = useState(null);

  const loadSites = async () => {
    try {
      const data = await api.getSites();
      setSites(data);
      setActiveSite(prev => {
        // Aktif site listede hâlâ varsa koru, yoksa (silinmişse) ilk siteye geç.
        const stillExists = prev && data.some(s => s.id === prev.id);
        return stillExists ? prev : (data[0] ?? null);
      });
    } catch {}
  };

  useEffect(() => { if (isAuth) loadSites(); }, [isAuth]);

  if (!isAuth) return <Navigate to="/login" replace />;

  return (
    <div className="layout">
      <Sidebar
        sites={sites}
        activeSite={activeSite}
        onSiteChange={setActiveSite}
      />
      <main className="main-content">
        <Routes>
          <Route path="/"      element={<Dashboard site={activeSite} />} />
          <Route path="/sites" element={<Sites onSiteChange={loadSites} />} />
        </Routes>
      </main>
    </div>
  );
}

export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            <Route path="/login" element={<LoginGuard />} />
            <Route path="/*"    element={<ProtectedLayout />} />
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

// Zaten giriş yapmışsa dashboard'a yönlendir.
function LoginGuard() {
  const { isAuth } = useAuth();
  return isAuth ? <Navigate to="/" replace /> : <Login />;
}
