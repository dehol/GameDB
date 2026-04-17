import { useState, useEffect, useRef } from 'react';
import { Card, Button, Descriptions, message, Progress, Form, InputNumber, Input, Switch } from 'antd';
import { SyncOutlined, ImportOutlined } from '@ant-design/icons';
import { api } from '../../api';

export default function SyncPage() {
  const [steamResult, setSteamResult] = useState(null);
  const [gogResult, setGogResult] = useState(null);
  const [importResult, setImportResult] = useState(null);
  const [loading, setLoading] = useState(null);
  const [importOptions, setImportOptions] = useState({
    limit: null,
    igdbGameIdsText: '',
    overwriteExisting: false
  });
  const pollRef = useRef(null);

  useEffect(() => () => {
    if (pollRef.current) clearInterval(pollRef.current);
  }, []);

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
          {result.jobId !== undefined && <Descriptions.Item label="Job ID">{result.jobId}</Descriptions.Item>}
          {result.status !== undefined && <Descriptions.Item label="Status">{result.status}</Descriptions.Item>}
          {result.currentPhase !== undefined && (
            <Descriptions.Item label="Phase">{result.currentPhase}</Descriptions.Item>
          )}
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
          {result.errorCount !== undefined && <Descriptions.Item label="Errors">{result.errorCount}</Descriptions.Item>}
          {result.imported !== undefined && <Descriptions.Item label="Imported">{result.imported}</Descriptions.Item>}
          {result.updated !== undefined && <Descriptions.Item label="Updated">{result.updated}</Descriptions.Item>}
          {result.errors !== undefined && <Descriptions.Item label="Errors">{result.errors}</Descriptions.Item>}
          {result.scanned !== undefined && <Descriptions.Item label="Scanned">{result.scanned}</Descriptions.Item>}
        </Descriptions>
      )}
    </Card>
  );

  return (
    <div>
      <h2>Price Sync</h2>
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
    </div>
  );
}
