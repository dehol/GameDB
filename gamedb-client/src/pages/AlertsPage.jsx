import { useState, useEffect } from 'react';
import { Table, Tag, Button, Popconfirm, Modal, InputNumber, message } from 'antd';
import { DeleteOutlined, EditOutlined } from '@ant-design/icons';
import { api } from '../api';

export default function AlertsPage() {
  const [alerts, setAlerts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [editModal, setEditModal] = useState(null);
  const [targetPrice, setTargetPrice] = useState(null);
  const [targetDiscount, setTargetDiscount] = useState(null);

  const fetch = () => {
    setLoading(true);
    api.getAlerts().then(setAlerts).catch(e => message.error(e.message)).finally(() => setLoading(false));
  };
  useEffect(fetch, []);

  const remove = async (id) => {
    try { await api.deleteAlert(id); message.success('Deleted'); fetch(); }
    catch (e) { message.error(e.message); }
  };

  const openEdit = (alert) => {
    setEditModal(alert);
    setTargetPrice(alert.target_price);
    setTargetDiscount(alert.target_discount);
  };

  const saveEdit = async () => {
    try {
      await api.updateAlert(editModal.alert_id, { targetPrice, targetDiscount });
      message.success('Updated');
      setEditModal(null);
      fetch();
    } catch (e) { message.error(e.message); }
  };

  const columns = [
    { title: 'Game', dataIndex: 'game_title', key: 'game' },
    { title: 'Target Price', dataIndex: 'target_price', key: 'tp', render: v => v ? `$${Number(v).toFixed(2)}` : '—' },
    { title: 'Target Discount', dataIndex: 'target_discount', key: 'td', render: v => v ? `${v}%` : '—' },
    { title: 'Current Price', dataIndex: 'best_current_price', key: 'cp', render: v => v != null ? `$${Number(v).toFixed(2)}` : '—' },
    { title: 'Gap', dataIndex: 'price_gap_pct', key: 'gap', render: v => v != null ? `${v > 0 ? '+' : ''}${v}%` : '—' },
    { title: 'Status', dataIndex: 'is_active', key: 'status', render: (v, r) => {
      if (r.triggered_at) return <Tag color="green">Triggered</Tag>;
      return v ? <Tag color="blue">Active</Tag> : <Tag>Inactive</Tag>;
    }},
    { title: '', key: 'actions', render: (_, r) => (
      <span>
        <Button icon={<EditOutlined />} size="small" onClick={() => openEdit(r)} style={{ marginRight: 8 }} />
        <Popconfirm title="Delete alert?" onConfirm={() => remove(r.alert_id)}>
          <Button danger icon={<DeleteOutlined />} size="small" />
        </Popconfirm>
      </span>
    )},
  ];

  return (
    <div>
      <h2>My Alerts</h2>
      <Table dataSource={alerts} columns={columns} rowKey="alert_id" loading={loading} />
      <Modal title="Edit Alert" open={!!editModal} onCancel={() => setEditModal(null)} onOk={saveEdit}>
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
