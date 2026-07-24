// API istemcisi, tüm HTTP çağrıları buradan geçer.
// JWT token otomatik eklenir, 401 gelince token yenilenir.

// Geliştirmede: Vite proxy /api isteklerini localhost:5000'e yönlendirir -> BASE boş.
// Production'da: VITE_API_URL env değişkeni ile tam URL verilir (örn: https://api.siten.com).
const BASE = import.meta.env.VITE_API_URL || '';

// Token'ları localStorage'da tut
const getAccess  = () => localStorage.getItem('access_token');
const getRefresh = () => localStorage.getItem('refresh_token');
const setTokens  = (access, refresh) => {
  localStorage.setItem('access_token', access);
  if (refresh) localStorage.setItem('refresh_token', refresh);
};
const clearTokens = () => {
  localStorage.removeItem('access_token');
  localStorage.removeItem('refresh_token');
};

// Token yenileme bayrağı: paralel isteklerde birden fazla refresh önler
let isRefreshing = false;
let refreshQueue = [];

const processQueue = (error, token = null) => {
  refreshQueue.forEach(p => error ? p.reject(error) : p.resolve(token));
  refreshQueue = [];
};

async function request(path, options = {}) {
  const headers = {
    'Content-Type': 'application/json',
    ...options.headers,
  };

  const access = getAccess();
  if (access) headers['Authorization'] = `Bearer ${access}`;

  const res = await fetch(`${BASE}${path}`, {
    ...options,
    headers,
    body: options.body ? JSON.stringify(options.body) : undefined,
  });

  // 401 -> token yenile ve isteği tekrarla
  if (res.status === 401 && path !== '/api/auth/refresh') {
    if (isRefreshing) {
      // Başka bir yenileme sürüyorsa sıraya gir
      return new Promise((resolve, reject) => {
        refreshQueue.push({ resolve, reject });
      }).then(token => {
        headers['Authorization'] = `Bearer ${token}`;
        return fetch(`${BASE}${path}`, {
          ...options,
          headers,
          body: options.body ? JSON.stringify(options.body) : undefined,
        });
      });
    }

    isRefreshing = true;
    const refreshToken = getRefresh();

    if (!refreshToken) {
      clearTokens();
      window.location.href = '/login';
      return;
    }

    try {
      const r = await fetch(`${BASE}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });

      if (!r.ok) throw new Error('Refresh failed');

      const data = await r.json();
      setTokens(data.accessToken, data.refreshToken);
      processQueue(null, data.accessToken);

      headers['Authorization'] = `Bearer ${data.accessToken}`;
      return fetch(`${BASE}${path}`, {
        ...options,
        headers,
        body: options.body ? JSON.stringify(options.body) : undefined,
      });
    } catch (err) {
      processQueue(err, null);
      clearTokens();
      window.location.href = '/login';
    } finally {
      isRefreshing = false;
    }
  }

  return res;
}

// ── Auth ──────────────────────────────────────────────────────────────
export const api = {
  login: async (email, password) => {
    const res = await fetch(`${BASE}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Giriş başarısız.');
    setTokens(data.accessToken, data.refreshToken);
    return data;
  },

  logout: async () => {
    const refresh = getRefresh();
    if (refresh) {
      await fetch(`${BASE}/api/auth/logout`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: refresh }),
      }).catch(() => {});
    }
    clearTokens();
  },

  // ── Sites ──────────────────────────────────────────────────────────
  getSites: async () => {
    const res = await request('/api/sites');
    if (!res.ok) throw new Error('Siteler alınamadı.');
    return res.json();
  },

  createSite: async (name, domain) => {
    const res = await request('/api/sites', {
      method: 'POST',
      body: { name, domain },
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Site eklenemedi.');
    return data;
  },

  deleteSite: async (id) => {
    const res = await request(`/api/sites/${id}`, { method: 'DELETE' });
    if (!res.ok) throw new Error('Site silinemedi.');
  },

  // ── Stats ──────────────────────────────────────────────────────────
  getStats: async (siteId, days = 30) => {
    const res = await request(`/api/stats/${siteId}?days=${days}`);
    if (!res.ok) throw new Error('İstatistikler alınamadı.');
    return res.json();
  },

  getRealtime: async (siteId) => {
    const res = await request(`/api/stats/${siteId}/realtime`);
    if (!res.ok) return { active_visitors: 0 };
    return res.json();
  },

  isLoggedIn: () => !!getAccess(),
};
