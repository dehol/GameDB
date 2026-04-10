import { useState } from 'react';
import { Form, Input, Button, Card, Tabs, message } from 'antd';
import { UserOutlined, LockOutlined, MailOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { api } from '../api';
import { useAuth } from '../context/AuthContext';

export default function LoginPage() {
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState('login');
  const navigate = useNavigate();
  const { login } = useAuth();
  const [registerForm] = Form.useForm();

  const onLogin = async (values) => {
    setLoading(true);
    try {
      const res = await api.login(values);
      login(res.token, res.username, res.role);
      message.success(`Welcome, ${res.username}!`);
      navigate('/');
    } catch (e) {
      message.error(e.message);
    }
    setLoading(false);
  };

  const onRegister = async (values) => {
    setLoading(true);
    try {
      await api.register(values);
      message.success('Registration successful! Please log in.');
      registerForm.resetFields();
      setActiveTab('login');
    } catch (e) {
      message.error(e.message);
    }
    setLoading(false);
  };

  return (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '80vh' }}>
      <Card style={{ width: 420 }}>
        <Tabs centered activeKey={activeTab} onChange={setActiveTab} items={[
          {
            key: 'login', label: 'Login',
            children: (
              <Form onFinish={onLogin} layout="vertical">
                <Form.Item name="email" rules={[{ required: true, type: 'email' }]}>
                  <Input prefix={<MailOutlined />} placeholder="Email" size="large" />
                </Form.Item>
                <Form.Item name="password" rules={[{ required: true, min: 6 }]}>
                  <Input.Password prefix={<LockOutlined />} placeholder="Password" size="large" />
                </Form.Item>
                <Button type="primary" htmlType="submit" loading={loading} block size="large">Log In</Button>
              </Form>
            )
          },
          {
            key: 'register', label: 'Register',
            children: (
              <Form form={registerForm} onFinish={onRegister} layout="vertical">
                <Form.Item name="username" rules={[{ required: true }]}>
                  <Input prefix={<UserOutlined />} placeholder="Username" size="large" />
                </Form.Item>
                <Form.Item name="email" rules={[{ required: true, type: 'email' }]}>
                  <Input prefix={<MailOutlined />} placeholder="Email" size="large" />
                </Form.Item>
                <Form.Item name="password" rules={[{ required: true, min: 6 }]}>
                  <Input.Password prefix={<LockOutlined />} placeholder="Password" size="large" />
                </Form.Item>
                <Button type="primary" htmlType="submit" loading={loading} block size="large">Register</Button>
              </Form>
            )
          }
        ]} />
      </Card>
    </div>
  );
}
