import { useEffect, useState } from 'react';
import { Button, List, Tag, Typography, message } from 'antd';
import { api } from '../api';

const { Text } = Typography;

export default function NotificationsPage() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [markingAll, setMarkingAll] = useState(false);

  const fetchNotifications = () => {
    setLoading(true);
    api.getNotifications()
      .then(setItems)
      .catch((e) => message.error(e.message))
      .finally(() => setLoading(false));
  };

  useEffect(fetchNotifications, []);

  const markRead = async (id) => {
    try {
      await api.markNotificationRead(id);
      setItems((prev) => prev.map((n) => (n.notificationId === id ? { ...n, isRead: true } : n)));
    } catch (e) {
      message.error(e.message);
    }
  };

  const markAllRead = async () => {
    setMarkingAll(true);
    try {
      await api.markAllNotificationsRead();
      setItems((prev) => prev.map((n) => ({ ...n, isRead: true })));
      message.success('All notifications marked as read');
    } catch (e) {
      message.error(e.message);
    }
    setMarkingAll(false);
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <h2>Notifications</h2>
        <Button onClick={markAllRead} loading={markingAll}>Mark all as read</Button>
      </div>
      <List
        loading={loading}
        dataSource={items}
        locale={{ emptyText: 'No notifications yet' }}
        renderItem={(item) => (
          <List.Item
            actions={!item.isRead ? [<Button key="read" type="link" onClick={() => markRead(item.notificationId)}>Mark as read</Button>] : []}
          >
            <List.Item.Meta
              title={(
                <>
                  <Tag color={item.isRead ? 'default' : 'blue'}>
                    {item.isRead ? 'Read' : 'New'}
                  </Tag>{' '}
                  {item.type}
                </>
              )}
              description={(
                <>
                  <Text type="secondary">{new Date(item.createdAt).toLocaleString()}</Text>
                  {item.payload ? <pre style={{ marginTop: 8, whiteSpace: 'pre-wrap' }}>{item.payload}</pre> : null}
                </>
              )}
            />
          </List.Item>
        )}
      />
    </div>
  );
}
