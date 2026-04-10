const API = import.meta.env.VITE_API_URL || 'http://localhost:5212/api';

function headers() {
  const token = localStorage.getItem('token');
  const h = { 'Content-Type': 'application/json' };
  if (token) h['Authorization'] = `Bearer ${token}`;
  return h;
}

async function request(path, options = {}) {
  const res = await fetch(`${API}${path}`, { headers: headers(), ...options });

  if (res.status === 401) {
    localStorage.removeItem('token');
    window.location.href = '/login';
    throw new Error('Session expired. Please log in again.');
  }

  if (res.status === 204) return null;

  const contentType = res.headers.get('content-type') || '';
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || res.statusText);
  }

  if (contentType.includes('application/json')) {
    return res.json();
  }
  const text = await res.text();
  return { message: text };
}

export const api = {
  // Auth
  login: (data) => request('/auth/login', { method: 'POST', body: JSON.stringify(data) }),
  register: (data) => request('/auth/register', { method: 'POST', body: JSON.stringify(data) }),

  // Games
  getGames: (params) => {
    const q = new URLSearchParams();
    if (params?.search) q.set('search', params.search);
    if (params?.genreId) q.set('genreId', params.genreId);
    if (params?.shopId) q.set('shopId', params.shopId);
    if (params?.page) q.set('page', params.page);
    if (params?.pageSize) q.set('pageSize', params.pageSize);
    return request(`/games?${q}`);
  },
  getGame: (id) => request(`/games/${id}`),
  getDealScore: (id) => request(`/games/${id}/deal-score`),
  createGame: (data) => request('/games', { method: 'POST', body: JSON.stringify(data) }),
  updateGame: (id, data) => request(`/games/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteGame: (id) => request(`/games/${id}`, { method: 'DELETE' }),

  // Wishlist
  getWishlist: () => request('/wishlist'),
  addToWishlist: (gameId) => request(`/wishlist/${gameId}`, { method: 'POST' }),
  removeFromWishlist: (gameId) => request(`/wishlist/${gameId}`, { method: 'DELETE' }),
  toggleWishlist: (gameId) => request(`/wishlist/${gameId}/toggle`, { method: 'POST' }),
  importSteam: () => request('/wishlist/import-steam', { method: 'POST' }),

  // Profile
  getProfile: () => request('/profile'),
  upsertShopProfile: (data) => request('/profile/shop-profile', { method: 'PUT', body: JSON.stringify(data) }),

  // Alerts
  getAlerts: () => request('/alerts'),
  createAlert: (data) => request('/alerts', { method: 'POST', body: JSON.stringify(data) }),
  updateAlert: (id, data) => request(`/alerts/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteAlert: (id) => request(`/alerts/${id}`, { method: 'DELETE' }),

  // Library
  getLibrary: () => request('/library'),
  addToLibrary: (data) => request('/library', { method: 'POST', body: JSON.stringify(data) }),

  // Admin: unified import pipeline
  startImportPipeline: () => request('/import/start', { method: 'POST' }),
  getImportPipelineStatus: (pipelineId) => request(`/import/status/${pipelineId}`),
  getCurrentImportPipeline: () => request('/import/current'),
};
