// gamedb-client/src/components/Layout.jsx
import React, { useContext } from 'react';
import { Outlet, useNavigate, Link } from 'react-router-dom';
import { Layout as AntLayout, Menu, Button } from 'antd';
import {
  AppstoreOutlined,
  HeartOutlined,
  BellOutlined,
  BookOutlined,
  UserOutlined,
  SettingOutlined,
  SyncOutlined,
} from '@ant-design/icons';
import { AuthContext } from '../context/AuthContext';

const { Header, Sider, Content } = AntLayout;

const Layout = () => {
  const { user, logout } = useContext(AuthContext);
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <AntLayout style={{ minHeight: '100vh' }}>
      {/* SIDEBAR */}
      <Sider width={200} theme="dark" style={{ background: '#141414' }}>
        <div
          style={{
            padding: '16px 24px',
            fontSize: '20px',
            fontWeight: 700,
            color: '#fff',
            letterSpacing: '1px',
          }}
        >
          GameDB
        </div>

        <Menu
          theme="dark"
          mode="inline"
          defaultSelectedKeys={['/']}
          style={{ background: '#141414', borderRight: 0 }}
        >
          <Menu.Item key="/" icon={<AppstoreOutlined />}>
            <Link to="/">Catalog</Link>
          </Menu.Item>

          <Menu.Item key="/wishlist" icon={<HeartOutlined />}>
            <Link to="/wishlist">Wishlist</Link>
          </Menu.Item>

          <Menu.Item key="/alerts" icon={<BellOutlined />}>
            <Link to="/alerts">Alerts</Link>
          </Menu.Item>

          <Menu.Item key="/library" icon={<BookOutlined />}>
            <Link to="/library">Library</Link>
          </Menu.Item>

          <Menu.Item key="/profile" icon={<UserOutlined />}>
            <Link to="/profile">Profile</Link>
          </Menu.Item>

          <Menu.Divider />

          <Menu.Item key="/admin/games" icon={<SettingOutlined />}>
            <Link to="/admin/games">Admin: Games</Link>
          </Menu.Item>

          <Menu.Item key="/admin/sync" icon={<SyncOutlined />}>
            <Link to="/admin/sync">Admin: Sync</Link>
          </Menu.Item>
        </Menu>
      </Sider>

      {/* MAIN CONTENT */}
      <AntLayout>
        {/* HEADER */}
        <Header
          style={{
            padding: '0 24px',
            background: '#141414',
            display: 'flex',
            justifyContent: 'flex-end',
            alignItems: 'center',
            height: 64,
          }}
        >
          <span style={{ color: '#fff', marginRight: 16, fontSize: 16 }}>
            {user ? `${user.username} (${user.role})` : 'Гість'}
          </span>
          <Button type="text" style={{ color: '#000000' }} onClick={handleLogout}>
            Logout
          </Button>
        </Header>

        {/* PAGE CONTENT */}
        <Content style={{ padding: '24px', background: '#1a1a1a', minHeight: 'calc(100vh - 64px)' }}>
          <Outlet />
        </Content>
      </AntLayout>
    </AntLayout>
  );
};

export default Layout;