import { Spin } from 'antd';
import { LoadingOutlined } from '@ant-design/icons';

export default function LoadingSpinner({ size = 'large', tip = '', fullPage = false }) {
  const icon = <LoadingOutlined style={{ fontSize: size === 'large' ? 36 : 20, color: 'var(--primary)' }} spin />;

  if (fullPage) {
    return (
      <div style={{
        display: 'flex', flexDirection: 'column', alignItems: 'center',
        justifyContent: 'center', minHeight: '40vh', gap: 16,
      }}>
        <Spin indicator={icon} />
        {tip && <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>{tip}</span>}
      </div>
    );
  }

  return (
    <div style={{ textAlign: 'center', padding: '40px 0' }}>
      <Spin indicator={icon} />
      {tip && <div style={{ color: 'var(--text-muted)', fontSize: 13, marginTop: 10 }}>{tip}</div>}
    </div>
  );
}
