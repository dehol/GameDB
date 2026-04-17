import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  Form,
  Input,
  InputNumber,
  Progress,
  Row,
  Space,
  Statistic,
  Switch,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import { ImportOutlined, PauseCircleOutlined, ReloadOutlined } from '@ant-design/icons';
import { api } from '../../api';

const { Text } = Typography;
const POLL_MS = 3000;
const HEARTBEAT_WARN_MS = 3 * 60 * 1000;

function getStatusColor(status) {
  if (status === 'completed') return 'success';
  if (status === 'failed') return 'error';
  if (status === 'cancelled') return 'warning';
  if (status === 'running') return 'processing';
  return 'default';
}

function fmtDate(value) {
  if (!value) return '—';
  return new Date(value).toLocaleString();
}

function fmtDuration(startedAt, completedAt) {
  if (!startedAt) return '—';
  const start = new Date(startedAt).getTime();
  const end = completedAt ? new Date(completedAt).getTime() : Date.now();
  const total = Math.max(0, Math.floor((end - start) / 1000));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  return `${h}h ${m}m ${s}s`;
}

function fmtEta(seconds) {
  if (!seconds || seconds <= 0) return '—';
  const total = Math.floor(seconds);
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  return `${h}h ${m}m ${s}s`;
}

export default function SyncPage() {
  const [loading, setLoading] = useState(false);
  const [actionLoading, setActionLoading] = useState(null);
  const [currentJob, setCurrentJob] = useState(null);
  const [history, setHistory] = useState([]);
  const [pagination, setPagination] = useState({ current: 1, pageSize: 10, total: 0 });
  const [filters, setFilters] = useState({ status: undefined });
  const [importOptions, setImportOptions] = useState({
    limit: null,
    igdbGameIdsText: '',
    overwriteExisting: false,
  });
  const pollRef = useRef(null);

  const fetchHistory = async (page = pagination.current, pageSize = pagination.pageSize, status = filters.status) => {
    const res = await api.getImportJobs({ page, pageSize, status });
    setHistory(res.jobs || []);
    setPagination({
      current: res.page || page,
      pageSize: res.pageSize || pageSize,
      total: res.total || 0,
    });
  };

  const fetchCurrent = async () => {
    const res = await api.getCurrentImportPipeline();
    if (res?.status === 'no_running_pipeline') {
      setCurrentJob(null);
      return null;
    }
    setCurrentJob(res);
    return res;
  };

  const refreshAll = async () => {
    setLoading(true);
    try {
      await Promise.all([fetchCurrent(), fetchHistory()]);
    } catch (e) {
      message.error(e.message);
    }
    setLoading(false);
  };

  useEffect(() => {
    refreshAll();
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, []);

  useEffect(() => {
    if (pollRef.current) clearInterval(pollRef.current);
    if (currentJob?.status === 'running' || currentJob?.status === 'pending') {
      pollRef.current = setInterval(async () => {
        try {
          const latest = await fetchCurrent();
          await fetchHistory();
          if (!latest || (latest.status !== 'running' && latest.status !== 'pending')) {
            if (pollRef.current) {
              clearInterval(pollRef.current);
              pollRef.current = null;
            }
          }
        } catch {
          if (pollRef.current) {
            clearInterval(pollRef.current);
            pollRef.current = null;
          }
        }
      }, POLL_MS);
    }
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, [currentJob?.pipelineId, currentJob?.status]);

  const startImport = async () => {
    if (currentJob?.status === 'running' || currentJob?.status === 'pending') {
      message.warning('Import pipeline is already active');
      return;
    }

    setActionLoading('start');
    try {
      const igdbGameIds = importOptions.igdbGameIdsText
        .split(/[\s,]+/)
        .map(Number)
        .filter((x) => Number.isInteger(x) && x > 0);

      const payload = {
        limit: importOptions.limit,
        igdbGameIds: igdbGameIds.length > 0 ? igdbGameIds : null,
        overwriteExisting: importOptions.overwriteExisting,
      };

      const res = await api.startImportPipeline(payload);
      message.success(`Import started (#${res.pipelineId})`);
      await refreshAll();
    } catch (e) {
      message.error(e.message);
    }
    setActionLoading(null);
  };

  const cancelImport = async () => {
    if (!currentJob?.pipelineId) return;
    setActionLoading('cancel');
    try {
      const res = await api.cancelImportPipeline(currentJob.pipelineId);
      message.info(res.message || 'Cancellation requested');
      await refreshAll();
    } catch (e) {
      message.error(e.message);
    }
    setActionLoading(null);
  };

  const retryLast = async () => {
    const latest = history[0];
    if (!latest) {
      message.warning('No previous import jobs');
      return;
    }
    setActionLoading('retry');
    setImportOptions((prev) => ({
      ...prev,
      overwriteExisting: Boolean(latest.totalGamesUpdated > 0 || latest.totalOffersUpdated > 0),
    }));
    await startImport();
    setActionLoading(null);
  };

  const isHeartbeatStale = useMemo(() => {
    if (!currentJob?.lastUpdatedAt) return false;
    return Date.now() - new Date(currentJob.lastUpdatedAt).getTime() > HEARTBEAT_WARN_MS;
  }, [currentJob?.lastUpdatedAt]);

  const storeRows = useMemo(() => {
    if (!currentJob) return [];
    return [
      { key: 'steam', name: 'Steam', ...(currentJob.steam || {}) },
      { key: 'gog', name: 'GOG', ...(currentJob.gog || {}) },
      { key: 'egs', name: 'Epic Games Store', ...(currentJob.egs || {}) },
    ];
  }, [currentJob]);

  const historyColumns = [
    {
      title: 'Job',
      dataIndex: 'importJobId',
      key: 'importJobId',
      width: 90,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 120,
      render: (value) => <Tag color={getStatusColor(value)}>{String(value).toUpperCase()}</Tag>,
    },
    {
      title: 'Phase',
      dataIndex: 'currentPhase',
      key: 'currentPhase',
      width: 160,
      render: (value) => value || '—',
    },
    {
      title: 'Progress',
      key: 'progress',
      width: 220,
      render: (_, row) => {
        const total = row.totalGames || 0;
        const processed = row.processedGames || 0;
        const pct = total > 0 ? Math.round((processed / total) * 100) : 0;
        return (
          <div>
            <Progress percent={pct} size="small" status={row.status === 'failed' ? 'exception' : 'normal'} />
            <Text type="secondary">{processed}/{total}</Text>
          </div>
        );
      },
    },
    {
      title: 'New/Updated/Skipped/Failed',
      key: 'metrics',
      width: 260,
      render: (_, row) => `${row.totalGamesCreated || 0} / ${row.totalGamesUpdated || 0} / ${row.totalGamesSkipped || 0} / ${row.totalGamesFailed || 0}`,
    },
    {
      title: 'Started',
      dataIndex: 'startedAt',
      key: 'startedAt',
      width: 180,
      render: fmtDate,
    },
    {
      title: 'Duration',
      key: 'duration',
      width: 120,
      render: (_, row) => fmtDuration(row.startedAt, row.completedAt),
    },
    {
      title: 'Error',
      dataIndex: 'errorMessage',
      key: 'errorMessage',
      ellipsis: true,
      render: (value) => value || '—',
    },
  ];

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Button icon={<ReloadOutlined />} onClick={refreshAll} loading={loading}>
          Refresh
        </Button>
        <Button
          type="primary"
          icon={<ImportOutlined />}
          onClick={startImport}
          loading={actionLoading === 'start'}
          disabled={currentJob?.status === 'running' || currentJob?.status === 'pending'}
        >
          Start Import
        </Button>
        <Button
          danger
          icon={<PauseCircleOutlined />}
          onClick={cancelImport}
          loading={actionLoading === 'cancel'}
          disabled={!currentJob?.pipelineId || (currentJob?.status !== 'running' && currentJob?.status !== 'pending')}
        >
          Cancel
        </Button>
        <Button onClick={retryLast} loading={actionLoading === 'retry'}>
          Retry Last
        </Button>
      </Space>

      <Card title="Import Options" style={{ marginBottom: 16 }}>
        <Form layout="vertical">
          <Row gutter={16}>
            <Col xs={24} md={8}>
              <Form.Item label="Limit games">
                <InputNumber
                  min={1}
                  style={{ width: '100%' }}
                  value={importOptions.limit}
                  onChange={(value) => setImportOptions((prev) => ({ ...prev, limit: value ?? null }))}
                  placeholder="No limit"
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item label="Specific IGDB IDs (comma or space separated)">
                <Input.TextArea
                  rows={1}
                  value={importOptions.igdbGameIdsText}
                  onChange={(e) => setImportOptions((prev) => ({ ...prev, igdbGameIdsText: e.target.value }))}
                  placeholder="e.g. 1942, 1020 7346"
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={4}>
              <Form.Item label="Overwrite existing" valuePropName="checked">
                <Switch
                  checked={importOptions.overwriteExisting}
                  onChange={(checked) => setImportOptions((prev) => ({ ...prev, overwriteExisting: checked }))}
                />
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>

      <Card title="Current Pipeline" style={{ marginBottom: 16 }}>
        {!currentJob && <Text type="secondary">No active pipeline</Text>}
        {currentJob && (
          <>
            <Space align="center" style={{ marginBottom: 12 }}>
              <Tag color={getStatusColor(currentJob.status)}>{String(currentJob.status).toUpperCase()}</Tag>
              <Text>Pipeline #{currentJob.pipelineId}</Text>
              <Text type="secondary">Phase: {currentJob.phase || '—'}</Text>
            </Space>
            <Progress
              percent={Math.round(currentJob.progressPercent || 0)}
              status={currentJob.status === 'failed' ? 'exception' : currentJob.status === 'running' ? 'active' : 'normal'}
            />

            {isHeartbeatStale && (
              <Alert
                style={{ marginTop: 12 }}
                type="warning"
                showIcon
                message="No heartbeat updates detected for 3+ minutes. Pipeline may be stalled."
              />
            )}

            <Row gutter={[16, 16]} style={{ marginTop: 12 }}>
              <Col xs={12} md={6}><Statistic title="Processed / Total" value={`${currentJob.processedGames || 0} / ${currentJob.totalGames || 0}`} /></Col>
              <Col xs={12} md={6}><Statistic title="New Games" value={currentJob.importedGames || 0} /></Col>
              <Col xs={12} md={6}><Statistic title="Updated Games" value={currentJob.updatedGames || 0} /></Col>
              <Col xs={12} md={6}><Statistic title="Skipped Games" value={currentJob.skippedGames || 0} /></Col>
              <Col xs={12} md={6}><Statistic title="Failed Games" value={currentJob.failedGames || 0} /></Col>
              <Col xs={12} md={6}><Statistic title="Created Offers" value={currentJob.createdOffers || 0} /></Col>
              <Col xs={12} md={6}><Statistic title="Updated Offers" value={currentJob.updatedOffers || 0} /></Col>
              <Col xs={12} md={6}><Statistic title="Failed Offers" value={currentJob.failedOffers || 0} /></Col>
              <Col xs={12} md={6}><Statistic title="ETA" value={fmtEta(currentJob.etaSeconds)} /></Col>
              <Col xs={12} md={6}><Statistic title="Duration" value={fmtDuration(currentJob.startedAt, currentJob.completedAt)} /></Col>
              <Col xs={12} md={6}><Statistic title="Heartbeat" value={fmtDate(currentJob.lastUpdatedAt)} /></Col>
              <Col xs={12} md={6}><Statistic title="Errors" value={currentJob.errorCount || 0} /></Col>
            </Row>

            {currentJob.errorMessage && (
              <Alert style={{ marginTop: 12 }} type="error" showIcon message={currentJob.errorMessage} />
            )}

            <Table
              style={{ marginTop: 16 }}
              size="small"
              pagination={false}
              dataSource={storeRows}
              columns={[
                { title: 'Store', dataIndex: 'name', key: 'name' },
                { title: 'Total', dataIndex: 'total', key: 'total' },
                { title: 'Processed', dataIndex: 'processed', key: 'processed' },
                { title: 'New', dataIndex: 'new', key: 'new' },
                { title: 'Updated', dataIndex: 'updated', key: 'updated' },
                { title: 'Skipped', dataIndex: 'skipped', key: 'skipped' },
                { title: 'Failed', dataIndex: 'failed', key: 'failed' },
              ]}
            />
          </>
        )}
      </Card>

      <Card title="Import History">
        <Space style={{ marginBottom: 12 }}>
          <Button onClick={() => { setFilters({ status: undefined }); fetchHistory(1, pagination.pageSize, undefined); }}>All</Button>
          <Button onClick={() => { setFilters({ status: 'completed' }); fetchHistory(1, pagination.pageSize, 'completed'); }}>Completed</Button>
          <Button onClick={() => { setFilters({ status: 'failed' }); fetchHistory(1, pagination.pageSize, 'failed'); }}>Failed</Button>
          <Button onClick={() => { setFilters({ status: 'cancelled' }); fetchHistory(1, pagination.pageSize, 'cancelled'); }}>Cancelled</Button>
        </Space>
        <Table
          rowKey="importJobId"
          dataSource={history}
          columns={historyColumns}
          pagination={pagination}
          onChange={(pager) => fetchHistory(pager.current, pager.pageSize, filters.status)}
        />
      </Card>
    </div>
  );
}
