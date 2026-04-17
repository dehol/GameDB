import { BrowserRouter, Routes, Route, Navigate, Link, useLocation } from 'react-router-dom';
import { ConfigProvider, Layout, Menu, Button, theme } from 'antd';
import {
  AppstoreOutlined, HeartOutlined, UserOutlined, BellOutlined,
  BookOutlined, SettingOutlined, SyncOutlined, LoginOutlined, LogoutOutlined,
  LoadingOutlined, NotificationOutlined
} from '@ant-design/icons';
import { AuthProvider, useAuth } from './context/AuthContext';

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
  if (isLoading) return <div style={{ textAlign: 'center', marginTop: 100 }}><LoadingOutlined style={{ fontSize: 32 }} /></div>;
  return isAuth ? children : <Navigate to="/login" />;
}

function AdminRoute({ children }) {
  const { user, isLoading } = useAuth();
  if (isLoading) return <div style={{ textAlign: 'center', marginTop: 100 }}><LoadingOutlined style={{ fontSize: 32 }} /></div>;
  if (!user) return <Navigate to="/login" />;
  if (user.role !== 'admin') return <Navigate to="/" />;
  return children;
}

function AppLayout() {
  const { user, isAuth, logout } = useAuth();
  const location = useLocation();

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

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider breakpoint="lg" collapsedWidth={0} style={{ background: '#141414' }}>
        <div style={{ padding: '16px 24px', fontSize: 20, fontWeight: 700, color: '#fff', letterSpacing: 1 }}>
          GameDB
        </div>
        <Menu theme="dark" mode="inline" selectedKeys={[location.pathname]} items={menuItems} />
      </Sider>
      <Layout>
        <Header style={{ padding: '0 24px', display: 'flex', justifyContent: 'flex-end', alignItems: 'center', background: '#141414' }}>
          {isAuth ? (
            <span style={{ color: '#fff' }}>
              {user.username} ({user.role})
              <Button type="text" icon={<LogoutOutlined />} onClick={logout} style={{ color: '#fff', marginLeft: 12 }}>Logout</Button>
            </span>
          ) : (
            <Link to="/login"><Button type="primary" icon={<LoginOutlined />}>Login</Button></Link>
          )}
        </Header>
        <Content style={{ padding: 24, background: '#1a1a1a', minHeight: 'calc(100vh - 64px)' }}>
          <Routes>
            <Route path="/" element={<CatalogPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/games/:id" element={<GameDetailPage />} />
            <Route path="/wishlist" element={<PrivateRoute><WishlistPage /></PrivateRoute>} />
            <Route path="/profile" element={<PrivateRoute><ProfilePage /></PrivateRoute>} />
            <Route path="/alerts" element={<PrivateRoute><AlertsPage /></PrivateRoute>} />
            <Route path="/notifications" element={<PrivateRoute><NotificationsPage /></PrivateRoute>} />
            <Route path="/library" element={<PrivateRoute><LibraryPage /></PrivateRoute>} />
            <Route path="/admin/games" element={<AdminRoute><GamesAdminPage /></AdminRoute>} />
            <Route path="/admin/sync" element={<AdminRoute><SyncPage /></AdminRoute>} />
          </Routes>
        </Content>
      </Layout>
    </Layout>
  );
}

export default function App() {
  return (
    <ConfigProvider theme={{ algorithm: theme.darkAlgorithm }}>
      <AuthProvider>
        <BrowserRouter>
          <AppLayout />
        </BrowserRouter>
      </AuthProvider>
    </ConfigProvider>
  );
}
