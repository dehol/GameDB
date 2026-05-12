import { useState, useEffect, useRef } from 'react';
import {
  Card, Button, Descriptions, message, Progress, Form, InputNumber, Input, Switch,
  Table, Modal, Tag, Space
} from 'antd';
import { SyncOutlined, ImportOutlined, EyeOutlined } from '@ant-design/icons';
import { api } from '../../api';

export default function SyncPage() {
  const [allResult, setAllResult] = useState(null);
  const [steamResult, setSteamResult] = useState(null);
  const [gogResult, setGogResult] = useState(null);
  const [importResult, setImportResult] = useState(null);
  const [loading, setLoading] = useState(null);
  const [jobs, setJobs] = useState([]);
  const [jobsLoading, setJobsLoading] = useState(false);
  const [jobDetails, setJobDetails] = useState(null);
  const [jobLogs, setJobLogs] = useState([]);
  const [isDetailsOpen, setIsDetailsOpen] = useState(false);
  const [importOptions, setImportOptions] = useState({
    limit: null,
    igdbGameIdsText: '',
    overwriteExisting: false
  });
  const pollRef = useRef(null);

  const loadJobs = async () => {
    setJobsLoading(true);
    try {
      const res = await api.getImportJobs({ page: 1, pageSize: 20 });
      setJobs(res.jobs || []);
    } catch (e) {
      message.error(e.message);
    } finally {
      setJobsLoading(false);
    }
  };

  useEffect(() => {
    loadJobs();
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, []);

  const openDetails = async (pipelineId) => {
    try {
      const [details, logsRes] = await Promise.all([
        api.getImportJobDetails(pipelineId),
        api.getImportJobLogs(pipelineId, { take: 200 }),
      ]);
      setJobDetails(details);
      setJobLogs(logsRes.logs || []);
      setIsDetailsOpen(true);
    } catch (e) {
      message.error(e.message);
    }
  };

  const syncSteam = async () => {
    setLoading('steam');
    try {
      const res = await api.syncSteam();
      setSteamResult(res);
      message.success('Steam sync completed');
    } catch (e) { message.error(e.message); }
    setLoading(null);
  };

  const syncGog = async () => {
    setLoading('gog');
    try {
      const res = await api.syncGog();
      setGogResult(res);
      message.success('GOG sync completed');
    } catch (e) { message.error(e.message); }
    setLoading(null);
  };

  const startUnifiedImport = async () => {
    setLoading('import');
    try {
      const igdbGameIds = importOptions.igdbGameIdsText
        .split(/[\s,]+/)
        .map(x => Number(x))
        .filter(x => Number.isInteger(x) && x > 0);

      const payload = {
        limit: importOptions.limit,
        igdbGameIds: igdbGameIds.length > 0 ? igdbGameIds : null,
        overwriteExisting: importOptions.overwriteExisting
      };

      const res = await api.startImportPipeline(payload);
      const pipelineId = res.pipelineId;
      message.success(`Import started (pipeline #${pipelineId}). Progress updates below.`);

      setImportResult({
        pipelineId,
        status: 'pending',
        currentPhase: 'starting',
        totalGames: 0,
        processedGames: 0,
        importedGames: 0,
        errorCount: 0,
      });

      if (pollRef.current) clearInterval(pollRef.current);
      pollRef.current = setInterval(async () => {
        try {
          const s = await api.getImportPipelineStatus(pipelineId);
          setImportResult(s);
          if (s.status === 'completed' || s.status === 'failed') {
            if (pollRef.current) {
              clearInterval(pollRef.current);
              pollRef.current = null;
            }
            setLoading(null);
            await loadJobs();
            if (s.status === 'completed') {
              message.success(`Done: ${s.importedGames} imported, ${s.errorCount} errors`);
            } else {
              message.error(s.errorMessage || 'Import pipeline failed');
            }
          }
        } catch (e) {
          if (pollRef.current) {
            clearInterval(pollRef.current);
            pollRef.current = null;
          }
          setLoading(null);
          message.error(e.message);
        }
      }, 2000);
    } catch (e) {
      message.error(e.message);
      setLoading(null);
    }
  };

  const ResultCard = ({ title, result, onSync, name, icon }) => (
    <Card title={title} style={{ marginBottom: 16 }}>
      <Button type="primary" icon={icon} onClick={onSync} loading={loading === name} size="large">
        {title}
      </Button>
      {result && (
        <Descriptions bordered column={1} style={{ marginTop: 16 }}>
          {result.pipelineId !== undefined && <Descriptions.Item label="Pipeline ID">{result.pipelineId}</Descriptions.Item>}
          {result.status !== undefined && <Descriptions.Item label="Status">{result.status}</Descriptions.Item>}
          {result.totalGames != null && result.totalGames > 0 && (
            <Descriptions.Item label="Progress">
              <Progress
                percent={Number((result.processedGames / result.totalGames) * 100)}
                status={result.status === 'failed' ? 'exception' : 'active'}
              />
              <div style={{ fontSize: 12, color: '#888', marginTop: 4 }}>
                {result.processedGames} / {result.totalGames}
              </div>
            </Descriptions.Item>
          )}
          {result.importedGames !== undefined && <Descriptions.Item label="Imported">{result.importedGames}</Descriptions.Item>}
          {result.updated !== undefined && <Descriptions.Item label="Updated">{result.updated}</Descriptions.Item>}
          {result.errors !== undefined && <Descriptions.Item label="Errors">{result.errors}</Descriptions.Item>}
          {result.scanned !== undefined && <Descriptions.Item label="Scanned">{result.scanned}</Descriptions.Item>}
        </Descriptions>
      )}
    </Card>
  );

  const columns = [
    { title: 'ID', dataIndex: 'importJobId', key: 'importJobId', width: 90 },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status) => <Tag color={status === 'completed' ? 'green' : status === 'failed' ? 'red' : 'blue'}>{status}</Tag>
    },
    { title: 'Phase', dataIndex: 'currentPhase', key: 'currentPhase' },
    { title: 'Games', dataIndex: 'totalGames', key: 'totalGames', width: 90 },
    { title: 'Created', dataIndex: 'totalGamesCreated', key: 'totalGamesCreated', width: 90 },
    { title: 'Offers+', dataIndex: 'totalOffersCreated', key: 'totalOffersCreated', width: 90 },
    { title: 'Offers~', dataIndex: 'totalOffersUpdated', key: 'totalOffersUpdated', width: 90 },
    { title: 'Errors', dataIndex: 'errorCount', key: 'errorCount', width: 90 },
    {
      title: 'Actions',
      key: 'actions',
      width: 110,
      render: (_, row) => (
        <Button icon={<EyeOutlined />} onClick={() => openDetails(row.importJobId)}>
          Details
        </Button>
      )
    }
  ];

  return (
    <div>
      <h2>Price Sync</h2>
      <ResultCard title="Sync All Shops" result={allResult} onSync={syncAll} name="all" icon={<SyncOutlined />} />
      <ResultCard title="Steam Sync" result={steamResult} onSync={syncSteam} name="steam" icon={<SyncOutlined />} />
      <ResultCard title="GOG Sync" result={gogResult} onSync={syncGog} name="gog" icon={<SyncOutlined />} />

      <h2 style={{ marginTop: 32 }}>Unified Import Pipeline</h2>
      <p style={{ color: '#888', marginBottom: 16 }}>
        Runs in the background via RAWG import pipeline. You can leave this page and return later to check status.
      </p>
      <Card style={{ marginBottom: 16 }}>
        <Form layout="vertical">
          <Form.Item label="Limit games to import">
            <InputNumber
              min={1}
              style={{ width: 240 }}
              value={importOptions.limit}
              onChange={(value) => setImportOptions(prev => ({ ...prev, limit: value ?? null }))}
              placeholder="No limit"
            />
          </Form.Item>
          <Form.Item label="Specific IGDB game IDs (comma or space separated)">
            <Input.TextArea
              rows={3}
              value={importOptions.igdbGameIdsText}
              onChange={(e) => setImportOptions(prev => ({ ...prev, igdbGameIdsText: e.target.value }))}
              placeholder="e.g. 1942, 1020 7346"
            />
          </Form.Item>
          <Form.Item label="Overwrite existing games" valuePropName="checked">
            <Switch
              checked={importOptions.overwriteExisting}
              onChange={(checked) => setImportOptions(prev => ({ ...prev, overwriteExisting: checked }))}
            />
          </Form.Item>
        </Form>
      </Card>
      <ResultCard
        title="Start Games Import"
        result={importResult}
        onSync={startUnifiedImport}
        name="import"
        icon={<ImportOutlined />}
      />

      <Card
        title="Import Jobs"
        extra={<Button onClick={loadJobs} loading={jobsLoading}>Refresh</Button>}
      >
        <Table
          rowKey="importJobId"
          loading={jobsLoading}
          columns={columns}
          dataSource={jobs}
          pagination={false}
          size="small"
        />
      </Card>

      <Modal
        title={jobDetails ? `Import Job #${jobDetails.importJobId}` : 'Import Job Details'}
        width={1000}
        open={isDetailsOpen}
        footer={null}
        onCancel={() => setIsDetailsOpen(false)}
      >
        {jobDetails && (
          <>
            <Descriptions bordered size="small" column={2} style={{ marginBottom: 16 }}>
              <Descriptions.Item label="Status">{jobDetails.status}</Descriptions.Item>
              <Descriptions.Item label="Phase">{jobDetails.currentPhase}</Descriptions.Item>
              <Descriptions.Item label="Total Games">{jobDetails.steamTotal}</Descriptions.Item>
              <Descriptions.Item label="Processed">{jobDetails.steamProcessed}</Descriptions.Item>
              <Descriptions.Item label="Games Created">{jobDetails.totalGamesCreated}</Descriptions.Item>
              <Descriptions.Item label="Offers Created">{jobDetails.totalOffersCreated}</Descriptions.Item>
              <Descriptions.Item label="Offers Updated">{jobDetails.totalOffersUpdated}</Descriptions.Item>
              <Descriptions.Item label="Errors">{jobDetails.errorCount}</Descriptions.Item>
              <Descriptions.Item label="Steam Offers Updated">{jobDetails.steamOffersUpdated}</Descriptions.Item>
              <Descriptions.Item label="GOG Offers Updated">{jobDetails.gogOffersUpdated}</Descriptions.Item>
              <Descriptions.Item label="EGS Offers Updated">{jobDetails.egsOffersUpdated}</Descriptions.Item>
              <Descriptions.Item label="Started">{new Date(jobDetails.startedAt).toLocaleString()}</Descriptions.Item>
            </Descriptions>

            <Card title="Import Logs" size="small">
              <Space direction="vertical" style={{ width: '100%' }}>
                {jobLogs.map((log) => (
                  <Card key={log.importJobLogId} size="small">
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <Space>
                        <Tag color={log.level === 'Error' ? 'red' : log.level === 'Warning' ? 'orange' : 'blue'}>{log.level}</Tag>
                        <Tag>{log.phase}</Tag>
                      </Space>
                      <span style={{ color: '#888' }}>{new Date(log.timestamp).toLocaleString()}</span>
                    </div>
                    <div style={{ marginTop: 8 }}>{log.message}</div>
                    {log.data && (
                      <pre style={{ marginTop: 8, whiteSpace: 'pre-wrap' }}>{log.data}</pre>
                    )}
                  </Card>
                ))}
                {jobLogs.length === 0 && <div>No logs available</div>}
              </Space>
            </Card>
          </>
        )}
      </Modal>
    </div>
  );
}