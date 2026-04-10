import { useState, useEffect } from 'react';
import { Card, Button, Progress, Row, Col, Statistic, message } from 'antd';
import { PlayCircleOutlined } from '@ant-design/icons';
import { api } from '../../api';

export default function BulkImportPage() {
  const [job, setJob] = useState(null);
  const [loading, setLoading] = useState(false);

  const fetchStatus = async () => {
    try {
      const res = await api.getLatestImportJob();
      setJob(res);
      return res;
    } catch (e) {
      message.error(e.message);
      return null;
    }
  };

  const startImport = async () => {
    setLoading(true);
    try {
      const res = await api.startBulkImport();
      message.success(res.message);
      setJob(prev => ({ ...prev, status: 'running' }));
    } catch (e) {
      message.error(e.message);
    }
    setLoading(false);
  };

  useEffect(() => {
    fetchStatus();
  }, []);

  useEffect(() => {
    if (job?.status === 'running') {
      const interval = setInterval(fetchStatus, 3000);
      return () => clearInterval(interval);
    }
  }, [job?.status]);

  const getProgress = (processed, total) => {
    if (!total || total === 0) return 0;
    return Math.round((processed / total) * 100);
  };

  return (
    <div>
      <h2>Full Catalog Import</h2>
      <p style={{ color: '#666', marginBottom: 24 }}>
        Import all games from Steam, GOG, and Epic Games Store.
        This process may take several hours depending on the catalog size.
      </p>

      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}>
          <Card title="Steam" size="small" style={{ height: '100%' }}>
            <Progress 
              percent={getProgress(job?.steam?.processed, job?.steam?.total)} 
              status={job?.status === 'running' && job?.currentPhase?.includes('steam') ? 'active' : 'normal'}
            />
            <Row gutter={16}>
              <Col span={12}>
                <Statistic title="Total" value={job?.steam?.total || 0} />
              </Col>
              <Col span={12}>
                <Statistic title="Imported" value={job?.steam?.imported || 0} valueStyle={{ color: '#3f8600' }} />
              </Col>
            </Row>
          </Card>
        </Col>

        <Col xs={24} md={8}>
          <Card title="GOG" size="small" style={{ height: '100%' }}>
            <Progress 
              percent={getProgress(job?.gog?.processed, job?.gog?.total)} 
              status={job?.status === 'running' && job?.currentPhase?.includes('gog') ? 'active' : 'normal'}
            />
            <Row gutter={16}>
              <Col span={12}>
                <Statistic title="Total" value={job?.gog?.total || 0} />
              </Col>
              <Col span={12}>
                <Statistic title="Imported" value={job?.gog?.imported || 0} valueStyle={{ color: '#3f8600' }} />
              </Col>
            </Row>
          </Card>
        </Col>

        <Col xs={24} md={8}>
          <Card title="Epic Games Store" size="small" style={{ height: '100%' }}>
            <Progress 
              percent={getProgress(job?.egs?.processed, job?.egs?.total)} 
              status={job?.status === 'running' && job?.currentPhase?.includes('egs') ? 'active' : 'normal'}
            />
            <Row gutter={16}>
              <Col span={12}>
                <Statistic title="Total" value={job?.egs?.total || 0} />
              </Col>
              <Col span={12}>
                <Statistic title="Imported" value={job?.egs?.imported || 0} valueStyle={{ color: '#3f8600' }} />
              </Col>
            </Row>
          </Card>
        </Col>
      </Row>

      <Card style={{ marginTop: 24 }}>
        <Row gutter={24} align="middle">
          <Col flex="auto">
            <Row gutter={32}>
              <Col>
                <Statistic 
                  title="Overall Progress" 
                  value={Math.round(job?.overallProgress || 0)} 
                  suffix="%" 
                />
              </Col>
              <Col>
                <Statistic title="Games Created" value={job?.totalGamesCreated || 0} />
              </Col>
              <Col>
                <Statistic title="Offers Created" value={job?.totalOffersCreated || 0} />
              </Col>
              <Col>
                <Statistic 
                  title="Errors" 
                  value={job?.errorCount || 0} 
                  valueStyle={{ color: job?.errorCount > 0 ? '#cf1322' : undefined }} 
                />
              </Col>
            </Row>
          </Col>
          <Col>
            <Button 
              type="primary" 
              icon={<PlayCircleOutlined />} 
              onClick={startImport}
              loading={loading || job?.status === 'running'}
              size="large"
            >
              {job?.status === 'running' ? 'Importing...' : 'Start Full Import'}
            </Button>
          </Col>
        </Row>

        {job?.status === 'completed' && (
          <Row style={{ marginTop: 16 }}>
            <Col>
              <Statistic 
                title="Started" 
                value={new Date(job.startedAt).toLocaleString()} 
                valueStyle={{ fontSize: 14 }} 
              />
            </Col>
            <Col style={{ marginLeft: 32 }}>
              <Statistic 
                title="Completed" 
                value={new Date(job.completedAt).toLocaleString()} 
                valueStyle={{ fontSize: 14 }} 
              />
            </Col>
          </Row>
        )}

        {job?.status === 'failed' && (
          <div style={{ marginTop: 16, color: '#cf1322' }}>
            <strong>Error:</strong> {job.errorMessage}
          </div>
        )}
      </Card>
    </div>
  );
}
