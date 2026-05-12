import { useEffect, useState } from 'react';
import { Card, Table, Form, Input, InputNumber, DatePicker, Button, Space, Tag } from 'antd';
import dayjs from 'dayjs';
import { api } from '../../api';

export default function AuditLogPage() {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState([]);

  const load = async (overrides = {}) => {
    setLoading(true);
    const values = form.getFieldsValue();

    try {
      const res = await api.getAuditLogs({
        page: 1,
        pageSize: 100,
        userId: values.userId || undefined,
        actionType: values.actionType || undefined,
        from: values.range?.[0] ? dayjs(values.range[0]).toISOString() : undefined,
        to: values.range?.[1] ? dayjs(values.range[1]).toISOString() : undefined,
        search: values.search || undefined,
        ...overrides,
      });
      setItems(res.items || []);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const columns = [
    { title: 'ID', dataIndex: 'auditLogId', key: 'auditLogId', width: 90 },
    {
      title: 'User',
      key: 'user',
      width: 180,
      render: (_, row) => row.username ? `${row.username} (#${row.userId})` : `#${row.userId ?? 'n/a'}`
    },
    {
      title: 'Action',
      dataIndex: 'actionType',
      key: 'actionType',
      width: 180,
      render: (value) => <Tag color="blue">{value}</Tag>
    },
    { title: 'Entity', dataIndex: 'entityId', key: 'entityId', width: 140 },
    {
      title: 'Timestamp',
      dataIndex: 'timestamp',
      key: 'timestamp',
      width: 180,
      render: (value) => new Date(value).toLocaleString()
    },
    { title: 'IP', dataIndex: 'ipAddress', key: 'ipAddress', width: 140 },
    {
      title: 'Changes',
      key: 'changes',
      render: (_, row) => (
        <div>
          {row.oldValue && <pre style={{ margin: 0, whiteSpace: 'pre-wrap' }}>old: {row.oldValue}</pre>}
          {row.newValue && <pre style={{ margin: 0, whiteSpace: 'pre-wrap' }}>new: {row.newValue}</pre>}
        </div>
      )
    }
  ];

  return (
    <div>
      <h2>Audit Log</h2>
      <Card style={{ marginBottom: 16 }}>
        <Form form={form} layout="vertical">
          <Space align="end" wrap>
            <Form.Item label="User ID" name="userId">
              <InputNumber min={1} placeholder="e.g. 3" />
            </Form.Item>
            <Form.Item label="Action Type" name="actionType">
              <Input placeholder="Import.Start / Game.Modified" style={{ width: 220 }} />
            </Form.Item>
            <Form.Item label="Date Range" name="range">
              <DatePicker.RangePicker showTime />
            </Form.Item>
            <Form.Item label="Search" name="search">
              <Input placeholder="Any field" style={{ width: 220 }} />
            </Form.Item>
            <Form.Item>
              <Space>
                <Button type="primary" onClick={() => load()}>Apply</Button>
                <Button onClick={() => { form.resetFields(); load(); }}>Reset</Button>
              </Space>
            </Form.Item>
          </Space>
        </Form>
      </Card>

      <Card>
        <Table rowKey="auditLogId" loading={loading} columns={columns} dataSource={items} pagination={{ pageSize: 25 }} />
      </Card>
    </div>
  );
}
