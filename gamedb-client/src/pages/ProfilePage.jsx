import { useState, useEffect } from 'react';
import { Card, Descriptions, Button, message, Space, Popconfirm, Alert } from 'antd';
import { LinkOutlined, DisconnectOutlined } from '@ant-design/icons';
import { api } from '../api';

const SHOPS = [
  { slug: 'steam', label: 'Steam', color: '#1b2838' },
  { slug: 'gog', label: 'GOG', color: '#86328a' },
  { slug: 'egs', label: 'Epic Games Store', color: '#0078f2' },
];

export default function ProfilePage() {
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [linking, setLinking] = useState(null);

  useEffect(() => {
    api.getProfile().then(setProfile).catch(e => message.error(e.message)).finally(() => setLoading(false));
  }, []);

  const linkShop = async (slug) => {
    setLinking(slug);
    try {
      const res = await api.getOAuthAuthorizeUrl(slug);
      if (res.url) {
        window.location.href = res.url;
      }
    } catch (e) {
      message.error(e.message);
    }
    setLinking(null);
  };

  const unlinkShop = async (shopId, shopName) => {
    const slug = SHOPS.find(s => {
      // Match by checking shop profiles
      return true; // Will be overridden below
    })?.slug;

    // Find the slug from shopId
    const shopSlug = shopId === 1 ? 'steam' : shopId === 2 ? 'gog' : 'egs';

    try {
      await api.unlinkShop(shopSlug);
      message.success(`${shopName} account unlinked`);
      api.getProfile().then(setProfile);
    } catch (e) {
      message.error(e.message);
    }
  };

  if (loading || !profile) return null;
  const isGuest = profile.role?.toLowerCase() === 'guest';

  const linkedShopIds = (profile.shopProfiles || []).map(sp => sp.shopId);

  return (
    <div>
      <Card title="Profile">
        <Descriptions bordered column={1}>
          <Descriptions.Item label="Username">{profile.username}</Descriptions.Item>
          <Descriptions.Item label="Email">{profile.email || '—'}</Descriptions.Item>
          <Descriptions.Item label="Role">{profile.role}</Descriptions.Item>
          <Descriptions.Item label="Registered">{new Date(profile.createdAt).toLocaleDateString()}</Descriptions.Item>
          <Descriptions.Item label="Last Login">{profile.lastLogin ? new Date(profile.lastLogin).toLocaleDateString() : '—'}</Descriptions.Item>
        </Descriptions>
      </Card>

      <Card title="Linked Shop Accounts" style={{ marginTop: 16 }}>
        {isGuest ? (
          <Alert message="Guest accounts cannot link shop profiles." type="info" showIcon />
        ) : (
          <Space direction="vertical" style={{ width: '100%' }} size="middle">
            {SHOPS.map(shop => {
              const linkedProfile = profile.shopProfiles?.find(sp =>
                (shop.slug === 'steam' && sp.shopId === 1) ||
                (shop.slug === 'gog' && sp.shopId === 2) ||
                (shop.slug === 'egs' && sp.shopId === 3)
              );
              const isLinked = !!linkedProfile;

              return (
                <div key={shop.slug} style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  padding: '12px 16px',
                  background: '#1f1f1f',
                  borderRadius: 8,
                  border: `1px solid ${isLinked ? shop.color : '#333'}`
                }}>
                  <div>
                    <span style={{ fontWeight: 600, fontSize: 15 }}>{shop.label}</span>
                    {isLinked && (
                      <span style={{ marginLeft: 12, color: '#52c41a', fontSize: 13 }}>
                        Connected ({linkedProfile.externalUid})
                      </span>
                    )}
                  </div>
                  {isLinked ? (
                    <Popconfirm
                      title={`Unlink ${shop.label} account?`}
                      description="You will need to re-link it to import your wishlist."
                      onConfirm={() => unlinkShop(linkedProfile.shopId, shop.label)}
                    >
                      <Button
                        danger
                        icon={<DisconnectOutlined />}
                        size="small"
                      >
                        Unlink
                      </Button>
                    </Popconfirm>
                  ) : (
                    <Button
                      type="primary"
                      icon={<LinkOutlined />}
                      onClick={() => linkShop(shop.slug)}
                      loading={linking === shop.slug}
                      style={{ background: shop.color, borderColor: shop.color }}
                    >
                      Link {shop.label}
                    </Button>
                  )}
                </div>
              );
            })}
          </Space>
        )}
      </Card>
    </div>
  );
}
