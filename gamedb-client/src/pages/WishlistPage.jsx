import { useState, useEffect } from 'react';
import { Table, Button, message, Popconfirm, Dropdown, Tag, Modal, Space } from 'antd';
import { DeleteOutlined, ImportOutlined, DownOutlined } from '@ant-design/icons';
import { api } from '../api';
import { useAuth } from '../context/AuthContext';

const SHOP_META = {
  steam: { label: 'Steam', color: '#1b2838' },
  gog: { label: 'GOG', color: '#86328a' },
  egs: { label: 'Epic Games', color: '#0078f2' },
};

export default function WishlistPage() {
  const { user } = useAuth();
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [importing, setImporting] = useState(null); // shop slug or null
  const [linkModal, setLinkModal] = useState(null); // { shop, authorizeUrl }

  const fetch = () => {
    setLoading(true);
    api.getWishlist().then(setItems).catch(e => message.error(e.message)).finally(() => setLoading(false));
  };

  useEffect(fetch, []);

  // Handle ?linked=shop query param after OAuth callback
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const linked = params.get('linked');
    if (linked) {
      window.history.replaceState({}, '', '/wishlist');
      if (linked === 'error') {
        const errMsg = params.get('message') || 'Unknown error';
        message.error(`Link failed: ${errMsg}`);
      } else {
        message.success(`${SHOP_META[linked]?.label || linked} account linked!`);
      }
    }
  }, []);

  const remove = async (gameId) => {
    try {
      await api.removeFromWishlist(gameId);
      message.success('Removed');
      fetch();
    } catch (e) { message.error(e.message); }
  };

  const handleImport = async (shop) => {
    setImporting(shop);
    try {
      const res = await api.importWishlist(shop);
      message.success(`Imported ${res.imported} games from ${SHOP_META[shop]?.label || shop}`);
      fetch();
    } catch (e) {
      // Check if error is 403 (not linked)
      if (e.message?.includes('not linked') || e.message?.includes('403')) {
        // Try to get authorize URL
        try {
          const authRes = await api.getOAuthAuthorizeUrl(shop);
          setLinkModal({ shop, authorizeUrl: authRes.url });
        } catch {
          message.error(`Please link your ${SHOP_META[shop]?.label || shop} account first.`);
        }
      } else {
        message.error(e.message);
      }
    }
    setImporting(null);
  };

  const shopMenuItems = Object.entries(SHOP_META).map(([slug, meta]) => ({
    key: slug,
    label: meta.label,
    onClick: () => handleImport(slug),
  }));

  const columns = [
    { title: 'Game', dataIndex: 'gameTitle', key: 'title' },
    { title: 'Added', dataIndex: 'addedAt', key: 'added', render: v => new Date(v).toLocaleDateString() },
    {
      title: 'Imported from',
      dataIndex: 'sourceShops',
      key: 'sources',
      render: (sources) => {
        if (!sources || sources.length === 0) return <Tag>Manual</Tag>;
        return (
          <Space size={[4, 4]} wrap>
            {sources.map(name => {
              const slug = name.toLowerCase().includes('steam') ? 'steam' :
                           name.toLowerCase().includes('gog') ? 'gog' :
                           name.toLowerCase().includes('epic') ? 'egs' : null;
              const meta = slug ? SHOP_META[slug] : null;
              return <Tag key={name} color={meta?.color}>{name}</Tag>;
            })}
          </Space>
        );
      }
    },
    { title: '', key: 'actions', render: (_, r) => (
      <Popconfirm title="Remove from wishlist?" onConfirm={() => remove(r.gameId)}>
        <Button danger icon={<DeleteOutlined />} size="small" />
      </Popconfirm>
    )},
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <h2>My Wishlist</h2>
        {user?.role !== 'guest' && (
          <Dropdown menu={{ items: shopMenuItems }} trigger={['click']}>
            <Button icon={<ImportOutlined />} loading={importing !== null}>
              Import Wishlist <DownOutlined />
            </Button>
          </Dropdown>
        )}
      </div>
      <Table dataSource={items} columns={columns} rowKey="gameId" loading={loading} />

      <Modal
        open={!!linkModal}
        title={`Link ${SHOP_META[linkModal?.shop]?.label || ''} Account`}
        onCancel={() => setLinkModal(null)}
        footer={null}
      >
        <p>To import your wishlist, you need to link your {SHOP_META[linkModal?.shop]?.label} account first.</p>
        <Button
          type="primary"
          onClick={() => {
            if (linkModal?.authorizeUrl) {
              window.location.href = linkModal.authorizeUrl;
            }
          }}
        >
          Link {SHOP_META[linkModal?.shop]?.label} Account
        </Button>
      </Modal>
    </div>
  );
}
