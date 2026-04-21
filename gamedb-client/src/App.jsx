import { BrowserRouter, Routes, Route, Navigate, Link, useLocation } from 'react-router-dom';
import { ConfigProvider, Layout, Menu, Button, Avatar, Badge, theme } from 'antd';
import {
  AppstoreOutlined, HeartOutlined, UserOutlined, BellOutlined,
  BookOutlined, SettingOutlined, SyncOutlined, LogoutOutlined,
  LoadingOutlined, NotificationOutlined, CrownOutlined,
} from '@ant-design/icons';
import { AuthProvider, useAuth } from './context/AuthContext';
import { antdThemeTokens } from './theme/theme';

import CatalogPage from './pages/CatalogPage';
import LoginPage from './pages/LoginPage';
import GameDetailPage from './pages/GameDetailPage';
import WishlistPage from './pages/WishlistPage';
import ProfilePage from './pages/ProfilePage';
import AlertsPage from './pages/AlertsPage';
import LibraryPage from './pages/LibraryPage';
import NotificationsPage from './pages/NotificationsPage';
import GamesAdminPage from './pages/admin/GamesAdminPage';
import SyncPage from './pages/admin/SyncPage';

const { Header, Content, Sider } = Layout;

function PrivateRoute({ children }) {
  const { isAuth, isLoading } = useAuth();
  if (isLoading) return <div style={{ textAlign: 'center', marginTop: 100 }}><LoadingOutlined style={{ fontSize: 32, color: 'var(--primary)' }} spin /></div>;
  return isAuth ? children : <Navigate to="/login" />;
}

function AdminRoute({ children }) {
  const { user, isLoading } = useAuth();
  if (isLoading) return <div style={{ textAlign: 'center', marginTop: 100 }}><LoadingOutlined style={{ fontSize: 32, color: 'var(--primary)' }} spin /></div>;
  if (!user) return <Navigate to="/login" />;
  if (user.role !== 'admin') return <Navigate to="/" />;
  return children;
}

function RoleBadge({ role }) {
  const styles = {
    admin: { bg: 'rgba(245,34,45,0.15)', color: 'var(--red)', border: '1px solid rgba(245,34,45,0.3)' },
    user:  { bg: 'rgba(79,156,249,0.12)', color: 'var(--primary)', border: '1px solid rgba(79,156,249,0.25)' },
    guest: { bg: 'rgba(152,152,176,0.12)', color: 'var(--text-muted)', border: '1px solid var(--border)' },
  };
  const s = styles[role] || styles.user;
  return (
    <span style={{
      ...s, padding: '1px 7px', borderRadius: 20,
      fontSize: 10, fontWeight: 700, letterSpacing: '0.05em', textTransform: 'uppercase',
      display: 'inline-flex', alignItems: 'center', gap: 4,
    }}>
      {role === 'admin' && <CrownOutlined style={{ fontSize: 9 }} />}
      {role}
    </span>
  );
}

function AppLayout() {
  const { user, isAuth, isLoading, logout } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: '100vh', background: 'var(--bg-base)' }}>
        <LoadingOutlined style={{ fontSize: 40, color: 'var(--primary)' }} spin />
      </div>
    );
  }

  if (!isAuth) {
    return (
      <Layout style={{ minHeight: '100vh', background: 'var(--bg-base)' }}>
        <Content style={{ background: 'var(--bg-base)', minHeight: '100vh' }}>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="*" element={<Navigate to="/login" replace />} />
          </Routes>
        </Content>
      </Layout>
    );
  }

  const menuItems = [
    { key: '/', icon: <AppstoreOutlined />, label: <Link to="/">Catalog</Link> },
  ];

  if (isAuth) {
    menuItems.push(
      { key: '/wishlist', icon: <HeartOutlined />, label: <Link to="/wishlist">Wishlist</Link> },
      { key: '/alerts', icon: <BellOutlined />, label: <Link to="/alerts">Alerts</Link> },
      { key: '/notifications', icon: <NotificationOutlined />, label: <Link to="/notifications">Notifications</Link> },
      { key: '/library', icon: <BookOutlined />, label: <Link to="/library">Library</Link> },
      { key: '/profile', icon: <UserOutlined />, label: <Link to="/profile">Profile</Link> },
    );
  }

  if (user?.role === 'admin') {
    menuItems.push(
      { type: 'divider' },
      { key: '/admin/games', icon: <SettingOutlined />, label: <Link to="/admin/games">Admin: Games</Link> },
      { key: '/admin/sync', icon: <SyncOutlined />, label: <Link to="/admin/sync">Admin: Sync</Link> },
    );
  }

  const avatarLetter = (user.username || 'U')[0].toUpperCase();
  const avatarColors = ['#4f9cf9', '#3dbf50', '#fa8c16', '#f5222d', '#722ed1'];
  const avatarColor = avatarColors[avatarLetter.charCodeAt(0) % avatarColors.length];

  return (
    <Layout style={{ minHeight: '100vh', background: 'var(--bg-base)' }}>
      <Sider
        breakpoint="lg"
        collapsedWidth={0}
        style={{
          background: 'var(--bg-elevated)',
          borderRight: '1px solid var(--border)',
          position: 'fixed',
          top: 0, left: 0, bottom: 0,
          zIndex: 100,
          overflow: 'hidden',
        }}
      >
        {/* Logo */}
        <div style={{
          padding: '20px 24px 16px',
          display: 'flex', alignItems: 'center', gap: 10,
          borderBottom: '1px solid var(--border)',
          marginBottom: 8,
        }}>
          <div style={{
            width: 32, height: 32, borderRadius: 8,
            background: 'linear-gradient(135deg, var(--primary), #7c3aed)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 16, fontWeight: 900, color: '#fff',
            flexShrink: 0,
          }}>G</div>
          <span style={{
            fontSize: 18, fontWeight: 800, color: 'var(--text-primary)',
            letterSpacing: '-0.02em', fontFamily: 'var(--font-display)',
          }}>GameDB</span>
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[location.pathname]}
          items={menuItems}
          style={{ background: 'transparent', border: 'none', padding: '0 8px' }}
        />
      </Sider>

      <Layout style={{ marginLeft: 200, background: 'var(--bg-base)', minHeight: '100vh' }}>
        {/* Top header bar */}
        <Header style={{
          padding: '0 24px', display: 'flex', justifyContent: 'flex-end', alignItems: 'center',
          background: 'var(--bg-elevated)', borderBottom: '1px solid var(--border)',
          position: 'sticky', top: 0, zIndex: 99,
          gap: 12,
          height: 'var(--nav-height)',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <Avatar
              size={32}
              style={{ background: avatarColor, fontWeight: 700, fontSize: 14, flexShrink: 0, cursor: 'default' }}
            >
              {avatarLetter}
            </Avatar>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2, lineHeight: 1 }}>
              <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>
                {user.username}
              </span>
              <RoleBadge role={user.role} />
            </div>
          </div>
          <div style={{ width: 1, height: 28, background: 'var(--border)', margin: '0 4px' }} />
          <Button
            type="text"
            icon={<LogoutOutlined />}
            onClick={logout}
            style={{
              color: 'var(--text-secondary)',
              display: 'flex', alignItems: 'center', gap: 4,
              fontWeight: 500,
            }}
          >
            Logout
          </Button>
        </Header>

        <Content style={{ padding: 24, background: 'var(--bg-base)' }}>
          <Routes>
            <Route path="/" element={<PrivateRoute><CatalogPage /></PrivateRoute>} />
            <Route path="/login" element={<Navigate to="/" replace />} />
            <Route path="/games/:id" element={<PrivateRoute><GameDetailPage /></PrivateRoute>} />
            <Route path="/wishlist" element={<PrivateRoute><WishlistPage /></PrivateRoute>} />
            <Route path="/profile" element={<PrivateRoute><ProfilePage /></PrivateRoute>} />
            <Route path="/alerts" element={<PrivateRoute><AlertsPage /></PrivateRoute>} />
            <Route path="/notifications" element={<PrivateRoute><NotificationsPage /></PrivateRoute>} />
            <Route path="/library" element={<PrivateRoute><LibraryPage /></PrivateRoute>} />
            <Route path="/admin/games" element={<AdminRoute><GamesAdminPage /></AdminRoute>} />
            <Route path="/admin/sync" element={<AdminRoute><SyncPage /></AdminRoute>} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </Content>
      </Layout>
    </Layout>
  );
}

export default function App() {
  return (
    <ConfigProvider theme={{ algorithm: theme.darkAlgorithm, token: antdThemeTokens }}>
      <AuthProvider>
        <BrowserRouter>
          <AppLayout />
        </BrowserRouter>
      </AuthProvider>
    </ConfigProvider>
  );
}
