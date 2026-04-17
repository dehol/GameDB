import { useState, useEffect } from 'react';
import { Table, Button, message, Popconfirm } from 'antd';
import { DeleteOutlined, ImportOutlined } from '@ant-design/icons';
import { api } from '../api';
import { useAuth } from '../context/AuthContext';

export default function WishlistPage() {
  const { user } = useAuth();
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [importing, setImporting] = useState(false);

  const fetch = () => {
    setLoading(true);
    api.getWishlist().then(setItems).catch(e => message.error(e.message)).finally(() => setLoading(false));
  };

  useEffect(fetch, []);

  const remove = async (gameId) => {
    try {
      await api.removeFromWishlist(gameId);
      message.success('Removed');
      fetch();
    } catch (e) { message.error(e.message); }
  };

  const importSteam = async () => {
    setImporting(true);
    try {
      const res = await api.importSteam();
      message.success(`Imported ${res.imported} games`);
      fetch();
    } catch (e) { message.error(e.message); }
    setImporting(false);
  };

  const columns = [
    { title: 'Game', dataIndex: 'gameTitle', key: 'title' },
    { title: 'Added', dataIndex: 'addedAt', key: 'added', render: v => new Date(v).toLocaleDateString() },
    { title: 'Source', dataIndex: 'sourceShop', key: 'source', render: v => v || 'Manual' },
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
          <Button icon={<ImportOutlined />} onClick={importSteam} loading={importing}>Import from Steam</Button>
        )}
      </div>
      <Table dataSource={items} columns={columns} rowKey="gameId" loading={loading} />
    </div>
  );
}
