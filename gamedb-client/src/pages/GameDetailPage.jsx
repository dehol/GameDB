import { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { Descriptions, Table, Tag, Spin, Card, Button, Modal, InputNumber, message } from 'antd';
import { BellOutlined } from '@ant-design/icons';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { api } from '../api';
import { useAuth } from '../context/AuthContext';

export default function GameDetailPage() {
  const { id } = useParams();
  const [game, setGame] = useState(null);
  const [loading, setLoading] = useState(true);
  const [alertModal, setAlertModal] = useState(false);
  const [targetPrice, setTargetPrice] = useState(null);
  const [targetDiscount, setTargetDiscount] = useState(null);
  const { isAuth } = useAuth();

  useEffect(() => {
    api.getGame(id).then(setGame).catch(e => message.error(e.message)).finally(() => setLoading(false));
  }, [id]);

  const createAlert = async () => {
    try {
      await api.createAlert({ gameId: Number(id), targetPrice, targetDiscount });
      message.success('Alert created!');
      setAlertModal(false);
    } catch (e) { message.error(e.message); }
  };

  if (loading) return <Spin size="large" style={{ display: 'block', margin: '100px auto' }} />;
  if (!game) return <div>Game not found</div>;

  const allPriceHistory = (game.offers || []).flatMap(o =>
    (o.priceHistory || []).map(ph => ({ ...ph, shop: o.shopName, rawDate: ph.recordedAt, recordedAt: new Date(ph.recordedAt).toLocaleDateString() }))
  ).sort((a, b) => new Date(a.rawDate) - new Date(b.rawDate));

  const offerColumns = [
    { title: 'Shop', dataIndex: 'shopName', key: 'shop' },
    { title: 'Price', dataIndex: 'currentPrice', key: 'price', render: v => `$${Number(v).toFixed(2)}` },
    { title: 'Discount', dataIndex: 'currentDiscount', key: 'disc', render: v => v > 0 ? <Tag color="red">-{v}%</Tag> : '—' },
    { title: 'Link', dataIndex: 'downloadUrl', key: 'link', render: v => v ? <a href={v} target="_blank" rel="noreferrer">Buy</a> : '—' },
  ];

  const dealColumns = [
    { title: 'Shop', dataIndex: 'shop_name', key: 'shop' },
    { title: 'Price', dataIndex: 'current_price', key: 'price', render: v => `$${Number(v).toFixed(2)}` },
    { title: 'Historical Low', dataIndex: 'historical_low', key: 'low', render: v => `$${Number(v).toFixed(2)}` },
    { title: 'Deal Score', dataIndex: 'deal_score', key: 'score', render: v => {
      const color = v >= 80 ? 'green' : v >= 50 ? 'orange' : 'default';
      return <Tag color={color}>{v}/100</Tag>;
    }},
    { title: 'Hist. Low?', dataIndex: 'is_historical_low', key: 'isLow', render: v => v ? <Tag color="green">YES</Tag> : 'No' },
  ];

  return (
    <div>
      <Card>
        <Descriptions title={game.title} bordered column={2}>
          <Descriptions.Item label="Developer">{game.developer || '—'}</Descriptions.Item>
          <Descriptions.Item label="Publisher">{game.publisher || '—'}</Descriptions.Item>
          <Descriptions.Item label="Release Date">{game.releaseDate || '—'}</Descriptions.Item>
          <Descriptions.Item label="Genres">
            {(game.genres || []).map(g => <Tag key={g}>{g}</Tag>)}
          </Descriptions.Item>
          <Descriptions.Item label="Description" span={2}>{game.description || '—'}</Descriptions.Item>
        </Descriptions>

        {isAuth && (
          <Button type="primary" icon={<BellOutlined />} onClick={() => setAlertModal(true)} style={{ marginTop: 16 }}>
            Set Price Alert
          </Button>
        )}
      </Card>

      <Card title="Offers" style={{ marginTop: 16 }}>
        <Table dataSource={game.offers || []} columns={offerColumns} rowKey="gameOfferId" pagination={false} />
      </Card>

      {game.dealScores?.length > 0 && (
        <Card title="Deal Scores" style={{ marginTop: 16 }}>
          <Table dataSource={game.dealScores} columns={dealColumns} rowKey="listing_id" pagination={false} />
        </Card>
      )}

      {allPriceHistory.length > 0 && (
        <Card title="Price History (6 months)" style={{ marginTop: 16 }}>
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={allPriceHistory}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="recordedAt" />
              <YAxis />
              <Tooltip />
              <Line type="monotone" dataKey="price" stroke="#1890ff" strokeWidth={2} dot={false} />
            </LineChart>
          </ResponsiveContainer>
        </Card>
      )}

      <Modal title="Set Price Alert" open={alertModal} onCancel={() => setAlertModal(false)} onOk={createAlert}>
        <div style={{ marginBottom: 16 }}>
          <label>Target Price ($):</label>
          <InputNumber min={0} step={0.01} value={targetPrice} onChange={setTargetPrice} style={{ width: '100%' }} />
        </div>
        <div>
          <label>Target Discount (%):</label>
          <InputNumber min={1} max={100} value={targetDiscount} onChange={setTargetDiscount} style={{ width: '100%' }} />
        </div>
      </Modal>
    </div>
  );
}
