import { useEffect, useState } from 'react';
import { Button, Input, InputNumber, notification, Space, Table, Typography, message } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { api } from '../../api';

export default function GamesManagementPage() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);
  const [search, setSearch] = useState('');
  const [offersCountFilter, setOffersCountFilter] = useState(null);
  const [selectedRowKeys, setSelectedRowKeys] = useState([]);
  const [syncLoading, setSyncLoading] = useState(false);

  const load = async (next = {}) => {
    setLoading(true);
    try {
      const res = await api.getAdminGamesManagement({
        search,
        offersCount: offersCountFilter,
        page: next.page ?? page,
        pageSize: next.pageSize ?? pageSize,
      });
      setItems(res.items || []);
      setTotalCount(res.totalCount || 0);
      setPage(res.page || 1);
      setPageSize(res.pageSize || 20);
    } catch (e) {
      message.error(e.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load({ page: 1 });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSearch = () => {
    load({ page: 1 });
  };

  const handleSyncOffers = async () => {
    setSyncLoading(true);
    try {
      const res = await api.syncOffersForGames(selectedRowKeys);
      notification.success({
        message: 'Синхронізацію запущено',
        description: res.message || `Завдання запущено для ${selectedRowKeys.length} ігор`,
      });
      setSelectedRowKeys([]);
    } catch (e) {
      message.error(e.message);
    } finally {
      setSyncLoading(false);
    }
  };

  const columns = [
    { title: 'ID', dataIndex: 'gameId', key: 'gameId', width: 90 },
    { title: 'Назва', dataIndex: 'title', key: 'title' },
    { title: 'Дата релізу', dataIndex: 'releaseDate', key: 'releaseDate', width: 160, render: (v) => v || '—' },
    { title: 'Кількість пропозицій', dataIndex: 'offersCount', key: 'offersCount', width: 190 },
  ];

  return (
    <div>
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          Управління іграми
        </Typography.Title>

        <Space wrap>
          <Input
            placeholder="Пошук за назвою"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onPressEnter={handleSearch}
            style={{ width: 260 }}
            allowClear
          />
          <InputNumber
            placeholder="Кількість пропозицій"
            value={offersCountFilter}
            onChange={setOffersCountFilter}
            style={{ width: 220 }}
            min={0}
          />
          <Button icon={<SearchOutlined />} onClick={handleSearch}>
            Фільтрувати
          </Button>
          <Button
            type="primary"
            onClick={handleSyncOffers}
            disabled={selectedRowKeys.length === 0}
            loading={syncLoading}
          >
            Знайти пропозиції
          </Button>
        </Space>

        <Table
          rowKey="gameId"
          loading={loading}
          dataSource={items}
          columns={columns}
          pagination={{
            current: page,
            pageSize,
            total: totalCount,
            showSizeChanger: true,
          }}
          onChange={(pagination) => {
            load({ page: pagination.current, pageSize: pagination.pageSize });
          }}
          rowSelection={{
            selectedRowKeys,
            onChange: setSelectedRowKeys,
          }}
        />
      </Space>
    </div>
  );
}
