import { useState, useEffect } from 'react';
import { Table, message } from 'antd';
import { api } from '../api';

export default function LibraryPage() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.getLibrary().then(setItems).catch(e => message.error(e.message)).finally(() => setLoading(false));
  }, []);

  const columns = [
    { title: 'Game', dataIndex: 'game_title', key: 'game' },
    { title: 'Shop', dataIndex: 'shop_name', key: 'shop' },
    { title: 'Developer', dataIndex: 'developer_name', key: 'dev' },
    { title: 'Price', dataIndex: 'purchase_store_price', key: 'price', render: v => v != null ? `$${Number(v).toFixed(2)}` : '—' },
    { title: 'Added', dataIndex: 'addedAt', key: 'added', render: v => new Date(v).toLocaleDateString() },
    { title: 'Link', dataIndex: 'shop_url', key: 'url', render: v => v ? <a href={v} target="_blank" rel="noreferrer">Visit</a> : '—' },
  ];

  return (
    <div>
      <h2>My Library</h2>
      <Table dataSource={items} columns={columns} rowKey={r => `${r.gameId}-${r.shop_name}`} loading={loading} />
    </div>
  );
}
