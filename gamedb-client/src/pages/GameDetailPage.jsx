import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Descriptions, Table, Tag, Card, Button, Modal, InputNumber, message, Space } from 'antd';
import { BellOutlined, ArrowLeftOutlined, StarOutlined, ShopOutlined } from '@ant-design/icons';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, ReferenceLine } from 'recharts';
import { api } from '../api';
import { useAuth } from '../context/AuthContext';
import LoadingSpinner from '../components/LoadingSpinner';
import EmptyState from '../components/EmptyState';

function coverHue(title = '') {
  return [...title].reduce((acc, c) => acc + c.charCodeAt(0), 0) % 360;
}

export default function GameDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [game, setGame] = useState(null);
  const [loading, setLoading] = useState(true);
  const [alertModal, setAlertModal] = useState(false);
  const [targetPrice, setTargetPrice] = useState(null);
  const [targetDiscount, setTargetDiscount] = useState(null);
  const [alertLoading, setAlertLoading] = useState(false);
  const { isAuth } = useAuth();

  useEffect(() => {
    api.getGame(id)
      .then(setGame)
      .catch(e => message.error(e.message))
      .finally(() => setLoading(false));
  }, [id]);

  const createAlert = async () => {
    setAlertLoading(true);
    try {
      await api.createAlert({ gameId: Number(id), targetPrice, targetDiscount });
      message.success('Price alert created!');
      setAlertModal(false);
    } catch (e) {
      message.error(e.message);
    }
    setAlertLoading(false);
  };

  if (loading) return <LoadingSpinner fullPage tip="Loading game..." />;
  if (!game) return (
    <EmptyState
      title="Game not found"
      description="This game doesn't exist or has been removed."
      action={() => navigate('/')}
      actionLabel="Back to catalog"
    />
  );

  const hue = coverHue(game.title || '');

  const allPriceHistory = (game.offers || []).flatMap(o =>
    (o.priceHistory || []).map(ph => ({
      ...ph,
      shop: o.shopName,
      rawDate: ph.recordedAt,
      recordedAt: new Date(ph.recordedAt).toLocaleDateString(),
    }))
  ).sort((a, b) => new Date(a.rawDate) - new Date(b.rawDate));

  const offerColumns = [
    { title: 'Shop', dataIndex: 'shopName', key: 'shop', render: v => <span style={{ fontWeight: 600 }}>{v}</span> },
    { title: 'Price', dataIndex: 'currentPrice', key: 'price', render: v => (
      <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--green)' }}>
        ${Number(v).toFixed(2)}
      </span>
    )},
    { title: 'Discount', dataIndex: 'currentDiscount', key: 'disc', render: v => v > 0
      ? <Tag color="red" style={{ fontWeight: 700 }}>-{v}%</Tag> : <span style={{ color: 'var(--text-muted)' }}>—</span>
    },
    { title: 'Link', dataIndex: 'downloadUrl', key: 'link', render: v => v
      ? <a href={v} target="_blank" rel="noreferrer" style={{ color: 'var(--primary)', fontWeight: 600 }}>Buy ↗</a>
      : <span style={{ color: 'var(--text-muted)' }}>—</span>
    },
  ];

  const dealColumns = [
    { title: 'Shop', dataIndex: 'shop_name', key: 'shop', render: v => <span style={{ fontWeight: 600 }}>{v}</span> },
    { title: 'Price', dataIndex: 'current_price', key: 'price', render: v => (
      <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700 }}>${Number(v).toFixed(2)}</span>
    )},
    { title: 'Historical Low', dataIndex: 'historical_low', key: 'low', render: v => (
      <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--green)' }}>${Number(v).toFixed(2)}</span>
    )},
    { title: 'Deal Score', dataIndex: 'deal_score', key: 'score', render: v => {
      const color = v >= 80 ? 'green' : v >= 50 ? 'orange' : 'default';
      return <Tag color={color} style={{ fontWeight: 700 }}>{v}/100</Tag>;
    }},
    { title: 'Hist. Low?', dataIndex: 'is_historical_low', key: 'isLow', render: v =>
      v ? <Tag color="green" style={{ fontWeight: 700 }}>YES</Tag> : <span style={{ color: 'var(--text-muted)' }}>No</span>
    },
  ];

  const minChartPrice = allPriceHistory.length
    ? Math.min(...allPriceHistory.map(p => Number(p.price)))
    : 0;

  return (
    <div style={{ maxWidth: 960, margin: '0 auto' }} className="page-enter">
      {/* Back button */}
      <Button
        type="text"
        icon={<ArrowLeftOutlined />}
        onClick={() => navigate(-1)}
        style={{ color: 'var(--text-secondary)', marginBottom: 16, paddingLeft: 0 }}
      >
        Back to catalog
      </Button>

      {/* Hero card */}
      <Card style={{ marginBottom: 16, overflow: 'hidden' }}>
        <div style={{ display: 'flex', gap: 24, alignItems: 'flex-start', flexWrap: 'wrap' }}>
          {/* Cover art */}
          <div style={{
            width: 120, height: 160, flexShrink: 0,
            borderRadius: 'var(--radius-lg)',
            background: `linear-gradient(145deg, hsl(${hue},35%,22%), hsl(${(hue + 60) % 360},22%,14%))`,
            border: '1px solid var(--border)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 48, fontWeight: 900, color: `hsl(${hue},60%,70%)`,
            boxShadow: 'var(--shadow-card)',
          }}>
            {(game.title || '?')[0].toUpperCase()}
          </div>

          {/* Info */}
          <div style={{ flex: 1, minWidth: 200 }}>
            <h1 style={{ fontSize: 24, fontWeight: 800, marginBottom: 6, letterSpacing: '-0.02em' }}>
              {game.title}
            </h1>

            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 12 }}>
              {(game.genres || []).map(g => (
                <Tag key={g} style={{ fontWeight: 500 }}>{g}</Tag>
              ))}
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '6px 24px', marginBottom: 16 }}>
              {[
                { label: 'Developer', value: game.developer?.name },
                { label: 'Publisher', value: game.publisher?.name },
                { label: 'Release Date', value: game.releaseDate },
              ].map(({ label, value }) => value ? (
                <div key={label}>
                  <div style={{ fontSize: 10, color: 'var(--text-muted)', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 2 }}>{label}</div>
                  <div style={{ fontSize: 13, color: 'var(--text-primary)', fontWeight: 500 }}>{value}</div>
                </div>
              ) : null)}
            </div>

            {game.description && (
              <p style={{ fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.7, marginBottom: 16, maxWidth: 560 }}>
                {game.description}
              </p>
            )}

            {isAuth && (
              <Space>
                <Button
                  type="primary"
                  icon={<BellOutlined />}
                  onClick={() => setAlertModal(true)}
                  style={{ fontWeight: 600 }}
                >
                  Set Price Alert
                </Button>
              </Space>
            )}
          </div>
        </div>
      </Card>

      {/* Offers */}
      <Card
        title={<span style={{ display: 'flex', alignItems: 'center', gap: 8 }}><ShopOutlined /> Offers</span>}
        style={{ marginBottom: 16 }}
      >
        {game.offers?.length > 0
          ? <Table dataSource={game.offers} columns={offerColumns} rowKey="gameOfferId" pagination={false} size="small" />
          : <EmptyState title="No offers available" description="No store listings found for this game." />
        }
      </Card>

      {/* Deal Scores */}
      {game.dealScores?.length > 0 && (
        <Card
          title={<span style={{ display: 'flex', alignItems: 'center', gap: 8 }}><StarOutlined /> Deal Scores</span>}
          style={{ marginBottom: 16 }}
        >
          <Table dataSource={game.dealScores} columns={dealColumns} rowKey="listing_id" pagination={false} size="small" />
        </Card>
      )}

      {/* Price history chart */}
      {allPriceHistory.length > 0 && (
        <Card title="Price History (6 months)" style={{ marginBottom: 16 }}>
          <ResponsiveContainer width="100%" height={280}>
            <LineChart data={allPriceHistory} margin={{ top: 8, right: 16, bottom: 8, left: 8 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
              <XAxis
                dataKey="recordedAt"
                tick={{ fontSize: 11, fill: 'var(--text-muted)' }}
                axisLine={{ stroke: 'var(--border)' }}
                tickLine={false}
              />
              <YAxis
                tick={{ fontSize: 11, fill: 'var(--text-muted)' }}
                axisLine={false}
                tickLine={false}
                tickFormatter={v => `$${v}`}
              />
              <Tooltip
                contentStyle={{
                  background: 'var(--bg-elevated)', border: '1px solid var(--border)',
                  borderRadius: 'var(--radius)', fontSize: 12,
                }}
                labelStyle={{ color: 'var(--text-muted)', marginBottom: 4 }}
                itemStyle={{ color: 'var(--primary)' }}
                formatter={v => [`$${Number(v).toFixed(2)}`, 'Price']}
              />
              {minChartPrice > 0 && (
                <ReferenceLine y={minChartPrice} stroke="var(--green)" strokeDasharray="4 4" label={{ value: 'Low', fill: 'var(--green)', fontSize: 10 }} />
              )}
              <Line
                type="monotone" dataKey="price" stroke="var(--primary)"
                strokeWidth={2} dot={false}
                activeDot={{ r: 4, fill: 'var(--primary)', stroke: 'var(--bg-card)', strokeWidth: 2 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </Card>
      )}

      {/* Price Alert Modal */}
      <Modal
        title={
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div style={{
              width: 32, height: 32, borderRadius: 8,
              background: 'var(--primary-dim)', border: '1px solid rgba(79,156,249,0.3)',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              color: 'var(--primary)',
            }}>
              <BellOutlined />
            </div>
            <span>Set Price Alert</span>
          </div>
        }
        open={alertModal}
        onCancel={() => setAlertModal(false)}
        onOk={createAlert}
        okText="Create Alert"
        okButtonProps={{ loading: alertLoading, style: { fontWeight: 600 } }}
        width={400}
      >
        <p style={{ color: 'var(--text-secondary)', fontSize: 13, marginBottom: 20, marginTop: 4 }}>
          We'll notify you when the price drops below your target.
        </p>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div>
            <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 6 }}>
              Target Price ($)
            </div>
            <InputNumber
              min={0} step={0.01} value={targetPrice} onChange={setTargetPrice}
              style={{ width: '100%' }} placeholder="e.g. 9.99"
              prefix="$"
            />
          </div>
          <div>
            <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 6 }}>
              OR Target Discount (%)
            </div>
            <InputNumber
              min={1} max={100} value={targetDiscount} onChange={setTargetDiscount}
              style={{ width: '100%' }} placeholder="e.g. 50"
              suffix="%"
            />
          </div>
        </div>
      </Modal>
    </div>
  );
}
