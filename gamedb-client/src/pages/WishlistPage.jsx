import { useState, useEffect } from 'react';
import { Table, Button, message, Popconfirm, Dropdown, Tag, Modal, Space, Input } from 'antd';
import { DeleteOutlined, ImportOutlined, DownOutlined } from '@ant-design/icons';
import { api } from '../api';
import { useAuth } from '../context/AuthContext';

const SHOP_META = {
  steam: { label: 'Steam', color: '#1b2838', placeholder: 'Steam64 ID' },
  gog: { label: 'GOG', color: '#86328a', placeholder: 'GOG username' },
};

export default function WishlistPage() {
  const { user } = useAuth();
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [importing, setImporting] = useState(null);
  const [linkModal, setLinkModal] = useState(null); // { shop, externalId }
  const [linkingExternal, setLinkingExternal] = useState(false);

  const fetchWishlist = () => {
    setLoading(true);
    api.getWishlist().then(setItems).catch(e => message.error(e.message)).finally(() => setLoading(false));
  };

  useEffect(fetchWishlist, []);

  const remove = async (gameId) => {
    try {
      await api.removeFromWishlist(gameId);
      message.success('Removed');
      fetchWishlist();
    } catch (e) { message.error(e.message); }
  };

  const handleImport = async (shop) => {
    setImporting(shop);
    try {
      const res = await api.importWishlist(shop);
      message.success(`Imported ${res.imported} games from ${SHOP_META[shop]?.label || shop}`);
      fetchWishlist();
    } catch (e) {
      if (e.status === 403) {
        // Account not linked — show direct input modal
        setLinkModal({ shop, externalId: '' });
      } else {
        message.error(e.message);
      }
    }
    setImporting(null);
  };

  const handleLinkByExternalId = async () => {
    if (!linkModal?.externalId?.trim()) {
      message.error('Please enter your ID');
      return;
    }
    setLinkingExternal(true);
    try {
      await api.linkShopByExternalId(linkModal.shop, linkModal.externalId.trim());
      message.success(`${SHOP_META[linkModal.shop]?.label} account linked!`);
      setLinkModal(null);
      // Retry import after linking
      handleImport(linkModal.shop);
    } catch (e) {
      message.error(e.message);
    }
    setLinkingExternal(false);
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
        onOk={handleLinkByExternalId}
        okText="Link & Import"
        confirmLoading={linkingExternal}
      >
        <p style={{ marginBottom: 12 }}>
          Enter your {SHOP_META[linkModal?.shop]?.label}{' '}
          {linkModal?.shop === 'gog' ? 'username' : 'Steam64 ID'} to link and import your wishlist.
        </p>
        <Input
          placeholder={SHOP_META[linkModal?.shop]?.placeholder}
          value={linkModal?.externalId || ''}
          onChange={e => setLinkModal(prev => ({ ...prev, externalId: e.target.value }))}
          onPressEnter={handleLinkByExternalId}
          size="large"
          autoFocus
        />
        {linkModal?.shop === 'gog' && (
          <p style={{ marginTop: 8, color: '#888', fontSize: 12 }}>
            Make sure your GOG wishlist is public for import to work.
          </p>
        )}
      </Modal>
    </div>
  );
}