import { useState, useEffect } from 'react';
import { Table, Button, Modal, Form, Input, DatePicker, Select, Popconfirm, message } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { api } from '../../api';

export default function GamesAdminPage() {
  const [games, setGames] = useState([]);
  const [loading, setLoading] = useState(true);
  const [modal, setModal] = useState(false);
  const [editId, setEditId] = useState(null);
  const [form] = Form.useForm();

  const fetch = () => {
    setLoading(true);
    api.getGames({ pageSize: 100 }).then(res => setGames(res.items || [])).catch(e => message.error(e.message)).finally(() => setLoading(false));
  };
  useEffect(fetch, []);

  const openAdd = () => { setEditId(null); form.resetFields(); setModal(true); };
  const openEdit = (game) => {
    setEditId(game.gameId);
    form.setFieldsValue({
      title: game.title,
      description: game.description,
      releaseDate: game.releaseDate ? dayjs(game.releaseDate) : null,
      genreIds: [],
    });
    setModal(true);
  };

  const onSubmit = async (values) => {
    const data = {
      title: values.title,
      description: values.description || null,
      releaseDate: values.releaseDate ? values.releaseDate.format('YYYY-MM-DD') : null,
      developerId: values.developerId || null,
      publisherId: values.publisherId || null,
      genreIds: values.genreIds || [],
    };
    try {
      if (editId) { await api.updateGame(editId, data); message.success('Updated'); }
      else { await api.createGame(data); message.success('Created'); }
      setModal(false);
      fetch();
    } catch (e) { message.error(e.message); }
  };

  const remove = async (id) => {
    try { await api.deleteGame(id); message.success('Deleted'); fetch(); }
    catch (e) { message.error(e.message); }
  };

  const columns = [
    { title: 'ID', dataIndex: 'gameId', key: 'id', width: 60 },
    { title: 'Title', dataIndex: 'title', key: 'title' },
    { title: 'Developer', dataIndex: 'developer_name', key: 'dev' },
    { title: 'Min Price', dataIndex: 'min_price', key: 'price', render: v => v != null ? `$${Number(v).toFixed(2)}` : '—' },
    { title: '', key: 'actions', width: 120, render: (_, r) => (
      <span>
        <Button icon={<EditOutlined />} size="small" onClick={() => openEdit(r)} style={{ marginRight: 8 }} />
        <Popconfirm title="Delete game?" onConfirm={() => remove(r.gameId)}>
          <Button danger icon={<DeleteOutlined />} size="small" />
        </Popconfirm>
      </span>
    )},
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <h2>Games Admin</h2>
        <Button type="primary" icon={<PlusOutlined />} onClick={openAdd}>Add Game</Button>
      </div>
      <Table dataSource={games} columns={columns} rowKey="gameId" loading={loading} />
      <Modal title={editId ? 'Edit Game' : 'Add Game'} open={modal} onCancel={() => setModal(false)} onOk={() => form.submit()}>
        <Form form={form} layout="vertical" onFinish={onSubmit}>
          <Form.Item name="title" label="Title" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="description" label="Description">
            <Input.TextArea rows={3} />
          </Form.Item>
          <Form.Item name="releaseDate" label="Release Date">
            <DatePicker style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="developerId" label="Developer ID">
            <Input type="number" />
          </Form.Item>
          <Form.Item name="publisherId" label="Publisher ID">
            <Input type="number" />
          </Form.Item>
          <Form.Item name="genreIds" label="Genre IDs">
            <Select mode="tags" placeholder="Enter genre IDs" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
