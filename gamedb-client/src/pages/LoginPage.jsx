import { useState } from 'react';
import { Form, Input, Button, Tabs, Divider, message, Alert } from 'antd';
import { UserOutlined, LockOutlined, MailOutlined, ThunderboltOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { api } from '../api';
import { useAuth } from '../context/AuthContext';

export default function LoginPage() {
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState('login');
  const [error, setError] = useState(null);
  const navigate = useNavigate();
  const { login } = useAuth();
  const [registerForm] = Form.useForm();

  const clearError = () => setError(null);

  const onLogin = async (values) => {
    setLoading(true);
    clearError();
    try {
      const res = await api.login(values);
      login(res.token, res.username, res.role);
      message.success(`Welcome back, ${res.username}!`);
      navigate('/');
    } catch (e) {
      setError(e.message);
    }
    setLoading(false);
  };

  const onRegister = async (values) => {
    setLoading(true);
    clearError();
    try {
      await api.register(values);
      message.success('Account created! Please log in.');
      registerForm.resetFields();
      setActiveTab('login');
    } catch (e) {
      setError(e.message);
    }
    setLoading(false);
  };

  const onGuestLogin = async () => {
    setLoading(true);
    clearError();
    try {
      if (!crypto?.randomUUID) {
        throw new Error('Your browser does not support secure guest session IDs.');
      }
      const deviceId = localStorage.getItem('deviceId') || crypto.randomUUID();
      const res = await api.loginGuest(deviceId);
      login(res.token, res.username, res.role, res.deviceId || deviceId);
      message.success(`Welcome, ${res.username}!`);
      navigate('/');
    } catch (e) {
      setError(e.message);
    }
    setLoading(false);
  };

  return (
    <div style={{
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      minHeight: '100vh', padding: 24,
      background: 'var(--bg-base)',
    }}>
      {/* Background decoration */}
      <div style={{
        position: 'fixed', inset: 0, pointerEvents: 'none', overflow: 'hidden', zIndex: 0,
      }}>
        <div style={{
          position: 'absolute', width: 500, height: 500, borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(79,156,249,0.06) 0%, transparent 70%)',
          top: '10%', left: '30%', transform: 'translate(-50%, -50%)',
        }} />
        <div style={{
          position: 'absolute', width: 400, height: 400, borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(124,58,237,0.05) 0%, transparent 70%)',
          bottom: '15%', right: '25%',
        }} />
      </div>

      <div style={{ position: 'relative', zIndex: 1, width: '100%', maxWidth: 420 }}>
        {/* Logo / Brand */}
        <div style={{ textAlign: 'center', marginBottom: 32 }}>
          <div style={{
            width: 56, height: 56, borderRadius: 14,
            background: 'linear-gradient(135deg, var(--primary), #7c3aed)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 28, fontWeight: 900, color: '#fff',
            boxShadow: '0 8px 24px rgba(79,156,249,0.3)',
            marginBottom: 16,
          }}>G</div>
          <h1 style={{ fontSize: 28, fontWeight: 800, letterSpacing: '-0.03em', marginBottom: 4 }}>
            GameDB
          </h1>
          <p style={{ color: 'var(--text-muted)', fontSize: 14 }}>
            Track prices across all major stores
          </p>
        </div>

        {/* Card */}
        <div style={{
          background: 'var(--bg-card)',
          border: '1px solid var(--border)',
          borderRadius: 'var(--radius-xl)',
          padding: '28px 32px',
          boxShadow: 'var(--shadow-lg)',
        }}>
          {/* Error banner */}
          {error && (
            <Alert
              message={error}
              type="error"
              showIcon
              closable
              onClose={clearError}
              style={{
                marginBottom: 20,
                borderRadius: 'var(--radius)',
                fontSize: 13,
              }}
            />
          )}

          <Tabs
            centered
            activeKey={activeTab}
            onChange={key => { setActiveTab(key); clearError(); }}
            items={[
              {
                key: 'login',
                label: 'Sign In',
                children: (
                  <>
                    <Form onFinish={onLogin} layout="vertical" style={{ marginTop: 8 }}>
                      <Form.Item
                        name="email"
                        rules={[{ required: true, message: 'Please enter your email' }, { type: 'email', message: 'Invalid email address' }]}
                        style={{ marginBottom: 14 }}
                      >
                        <Input
                          prefix={<MailOutlined style={{ color: 'var(--text-muted)' }} />}
                          placeholder="Email address"
                          size="large"
                        />
                      </Form.Item>
                      <Form.Item
                        name="password"
                        rules={[{ required: true, message: 'Please enter your password' }, { min: 6, message: 'At least 6 characters' }]}
                        style={{ marginBottom: 20 }}
                      >
                        <Input.Password
                          prefix={<LockOutlined style={{ color: 'var(--text-muted)' }} />}
                          placeholder="Password"
                          size="large"
                        />
                      </Form.Item>
                      <Button
                        type="primary" htmlType="submit" loading={loading}
                        block size="large"
                        style={{ fontWeight: 700, height: 44, fontSize: 15 }}
                      >
                        Sign In
                      </Button>
                    </Form>
                    <Divider plain style={{ color: 'var(--text-muted)', fontSize: 12 }}>or</Divider>
                    <Button
                      block size="large" onClick={onGuestLogin} loading={loading}
                      icon={<ThunderboltOutlined />}
                      style={{
                        background: 'var(--bg-active)', border: '1px solid var(--border)',
                        color: 'var(--text-secondary)', fontWeight: 600, height: 44,
                        transition: 'all var(--transition-fast)',
                      }}
                    >
                      Continue as Guest
                    </Button>
                  </>
                ),
              },
              {
                key: 'register',
                label: 'Sign Up',
                children: (
                  <Form form={registerForm} onFinish={onRegister} layout="vertical" style={{ marginTop: 8 }}>
                    <Form.Item
                      name="username"
                      rules={[{ required: true, message: 'Choose a username' }, { min: 3, message: 'At least 3 characters' }]}
                      style={{ marginBottom: 14 }}
                    >
                      <Input
                        prefix={<UserOutlined style={{ color: 'var(--text-muted)' }} />}
                        placeholder="Username"
                        size="large"
                      />
                    </Form.Item>
                    <Form.Item
                      name="email"
                      rules={[{ required: true, message: 'Please enter your email' }, { type: 'email', message: 'Invalid email address' }]}
                      style={{ marginBottom: 14 }}
                    >
                      <Input
                        prefix={<MailOutlined style={{ color: 'var(--text-muted)' }} />}
                        placeholder="Email address"
                        size="large"
                      />
                    </Form.Item>
                    <Form.Item
                      name="password"
                      rules={[{ required: true, message: 'Please create a password' }, { min: 6, message: 'At least 6 characters' }]}
                      style={{ marginBottom: 20 }}
                    >
                      <Input.Password
                        prefix={<LockOutlined style={{ color: 'var(--text-muted)' }} />}
                        placeholder="Password (min. 6 characters)"
                        size="large"
                      />
                    </Form.Item>
                    <Button
                      type="primary" htmlType="submit" loading={loading}
                      block size="large"
                      style={{ fontWeight: 700, height: 44, fontSize: 15 }}
                    >
                      Create Account
                    </Button>
                  </Form>
                ),
              },
            ]}
          />
        </div>

        {/* Footer note */}
        <p style={{ textAlign: 'center', color: 'var(--text-muted)', fontSize: 12, marginTop: 20 }}>
          Track game prices on Steam, GOG, Epic Games Store & more.
        </p>
      </div>
    </div>
  );
}
