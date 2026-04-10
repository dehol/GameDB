import { useState, useEffect } from 'react';
import { Card, Descriptions, Form, Input, Select, Button, message } from 'antd';
import { api } from '../api';

export default function ProfilePage() {
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.getProfile().then(setProfile).catch(e => message.error(e.message)).finally(() => setLoading(false));
  }, []);

  const onLink = async (values) => {
    try {
      await api.upsertShopProfile(values);
      message.success('Shop profile linked!');
      api.getProfile().then(setProfile);
    } catch (e) { message.error(e.message); }
  };

  if (loading || !profile) return null;

  return (
    <div>
      <Card title="Profile">
        <Descriptions bordered column={1}>
          <Descriptions.Item label="Username">{profile.username}</Descriptions.Item>
          <Descriptions.Item label="Email">{profile.email}</Descriptions.Item>
          <Descriptions.Item label="Role">{profile.role}</Descriptions.Item>
          <Descriptions.Item label="Registered">{new Date(profile.createdAt).toLocaleDateString()}</Descriptions.Item>
          <Descriptions.Item label="Last Login">{profile.lastLogin ? new Date(profile.lastLogin).toLocaleDateString() : '—'}</Descriptions.Item>
        </Descriptions>
      </Card>

      <Card title="Linked Shops" style={{ marginTop: 16 }}>
        {profile.shopProfiles?.length > 0 ? (
          <Descriptions bordered column={1}>
            {profile.shopProfiles.map(sp => (
              <Descriptions.Item key={sp.shopId} label={sp.shopName}>
                {sp.externalUid} (linked {new Date(sp.linkedAt).toLocaleDateString()})
              </Descriptions.Item>
            ))}
          </Descriptions>
        ) : <p>No shops linked yet.</p>}
      </Card>

      <Card title="Link Shop Account" style={{ marginTop: 16 }}>
        <Form onFinish={onLink} layout="inline">
          <Form.Item name="shopId" rules={[{ required: true }]}>
            <Select placeholder="Shop" style={{ width: 150 }}
              options={[{ value: 1, label: 'Steam' }, { value: 2, label: 'GOG' }]} />
          </Form.Item>
          <Form.Item name="externalUid" rules={[{ required: true }]}>
            <Input placeholder="External ID (Steam ID / GOG username)" style={{ width: 300 }} />
          </Form.Item>
          <Button type="primary" htmlType="submit">Link</Button>
        </Form>
      </Card>
    </div>
  );
}
