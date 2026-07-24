import { createContext, useContext, useState, useEffect } from 'react';
import { api } from '../api/client';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser]       = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Sayfa yenilendiğinde localStorage'daki token'dan kullanıcıyı geri yükle
    if (api.isLoggedIn()) {
      // Token var → payload'ı parse et (base64)
      try {
        // JWT payload'ı decode et
        // ÖNEMLİ: JWT base64url kullanır (- ve _ karakterleri var).
        // Standart atob() ise + ve / bekler → "Invalid character" hatası!
        // Çözüm: decode öncesi - → + ve _ → / yap.
        const token   = localStorage.getItem('access_token');
        const b64     = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
        const payload = JSON.parse(atob(b64));
        setUser({ id: payload.sub, email: payload.email, name: payload.name });
      } catch {
        // Bozuk token → logout
        api.logout();
      }
    }
    setLoading(false);
  }, []);

  const login = async (email, password) => {
    const data = await api.login(email, password);
    setUser({ email: data.email, name: data.fullName });
    return data;
  };

  const logout = async () => {
    await api.logout();
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, login, logout, isAuth: !!user }}>
      {!loading && children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
