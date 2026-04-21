const API = import.meta.env.VITE_API_URL || 'http://localhost:5212/api';

function buildHeaders(extraHeaders = {}) {
  const token = localStorage.getItem('token');
  const headers = { 'Content-Type': 'application/json', ...extraHeaders };
  if (token) headers.Authorization = `Bearer ${token}`;
  return headers;
}

async function request(path, options = {}) {
  const res = await fetch(`${API}${path}`, {
    ...options,
    headers: buildHeaders(options.headers || {}),
  });

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
  loginGuest: (deviceId) => request('/auth/guest', {
    method: 'POST',
    headers: { 'X-Device-Id': deviceId },
    body: JSON.stringify({ deviceId }),
  }),
  register: (data) => request('/auth/register', { method: 'POST', body: JSON.stringify(data) }),

  // Games
  getGames: (params) => {
    const q = new URLSearchParams();
    if (params?.search) q.set('search', params.search);
    if (params?.genreId) q.set('genreId', params.genreId);
    if (params?.shopId) q.set('shopId', params.shopId);
    if (params?.sortBy) q.set('sortBy', params.sortBy);
    if (params?.contentType) q.set('contentType', params.contentType);
    if (params?.page) q.set('page', params.page);
    if (params?.pageSize) q.set('pageSize', params.pageSize);
    return request(`/games?${q}`);
  },
  getGame: (id) => request(`/games/${id}`),
  getDealScore: (id) => request(`/games/${id}/deal-score`),
  getGameCovers: (gameIds) => {
    const q = new URLSearchParams();
    (gameIds || []).forEach(id => q.append('gameIds', id));
    return request(`/games/covers?${q}`);
  },
  createGame: (data) => request('/games', { method: 'POST', body: JSON.stringify(data) }),
  updateGame: (id, data) => request(`/games/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteGame: (id) => request(`/games/${id}`, { method: 'DELETE' }),

  // Wishlist
  getWishlist: () => request('/wishlist'),
  addToWishlist: (gameId) => request(`/wishlist/${gameId}`, { method: 'POST' }),
  removeFromWishlist: (gameId) => request(`/wishlist/${gameId}`, { method: 'DELETE' }),
  toggleWishlist: (gameId) => request(`/wishlist/${gameId}/toggle`, { method: 'POST' }),
  importWishlist: (shop) => request(`/wishlist/import/${shop}`, { method: 'POST' }),

  // OAuth
  getOAuthAuthorizeUrl: (shop) => request(`/oauth/${shop}/authorize`),
  getOAuthStatus: (shop) => request(`/oauth/${shop}/status`),
  unlinkShop: (shop) => request(`/oauth/${shop}/unlink`, { method: 'DELETE' }),

  // Profile
  getProfile: () => request('/profile'),
  upsertShopProfile: (data) => request('/profile/shop-profile', { method: 'PUT', body: JSON.stringify(data) }),

  // Alerts
  getAlerts: () => request('/alerts'),
  createAlert: (data) => request('/alerts', { method: 'POST', body: JSON.stringify(data) }),
  updateAlert: (id, data) => request(`/alerts/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteAlert: (id) => request(`/alerts/${id}`, { method: 'DELETE' }),

  // Notifications
  getNotifications: () => request('/notifications'),
  markNotificationRead: (id) => request(`/notifications/${id}/read`, { method: 'POST' }),
  markAllNotificationsRead: () => request('/notifications/read-all', { method: 'POST' }),

  // Library
  getLibrary: () => request('/library'),
  addToLibrary: (data) => request('/library', { method: 'POST', body: JSON.stringify(data) }),

  // Admin: unified import pipeline
  startImportPipeline: (options = {}) => request('/import/start', { method: 'POST', body: JSON.stringify(options) }),
  getImportPipelineStatus: (pipelineId) => request(`/import/status/${pipelineId}`),
  getCurrentImportPipeline: () => request('/import/current'),
};
