import { Button } from 'antd';
import { InboxOutlined } from '@ant-design/icons';

export default function EmptyState({
  icon,
  title = 'Nothing here',
  description = '',
  action,
  actionLabel,
}) {
  return (
    <div style={{
      display: 'flex', flexDirection: 'column', alignItems: 'center',
      justifyContent: 'center', padding: '64px 24px', gap: 12,
      textAlign: 'center',
    }}>
      <div style={{
        width: 72, height: 72, borderRadius: '50%',
        background: 'var(--bg-active)', border: '1px solid var(--border)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 30, color: 'var(--text-muted)', marginBottom: 4,
      }}>
        {icon || <InboxOutlined />}
      </div>
      <div style={{ fontSize: 16, fontWeight: 600, color: 'var(--text-secondary)' }}>{title}</div>
      {description && (
        <div style={{ fontSize: 13, color: 'var(--text-muted)', maxWidth: 340, lineHeight: 1.6 }}>
          {description}
        </div>
      )}
      {action && actionLabel && (
        <Button type="primary" onClick={action} style={{ marginTop: 8 }}>
          {actionLabel}
        </Button>
      )}
    </div>
  );
}
