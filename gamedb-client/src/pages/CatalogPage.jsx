import { useState, useEffect, useCallback } from 'react';
import { Card, Input, Select, Pagination, Tag, Button, Row, Col, Spin, Badge, message } from 'antd';
import { HeartOutlined, HeartFilled, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { api } from '../api';
import { useAuth } from '../context/AuthContext';

export default function CatalogPage() {
  const [games, setGames] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [genreId, setGenreId] = useState(null);
  const [shopId, setShopId] = useState(null);
  const [page, setPage] = useState(1);
  const [genres, setGenres] = useState([]);
  const [wishlistGameIds, setWishlistGameIds] = useState(new Set());
  const navigate = useNavigate();
  const { isAuth } = useAuth();

  // Load genres
  useEffect(() => {
    api.getGames({ pageSize: 1 }).then(() => {
      // We'll get genres from a separate endpoint or from game data
      // For now, hardcode the genres that exist in DB
      setGenres([
        { value: 1, label: 'RPG' },
        { value: 2, label: 'Action' },
        { value: 3, label: 'Adventure' },
        { value: 4, label: 'Roguelike' },
        { value: 5, label: 'Open World' },
        { value: 6, label: 'FPS' },
        { value: 7, label: 'Indie' },
        { value: 8, label: 'Strategy' },
      ]);
    });
  }, []);

  // Load wishlist for highlighting hearts
  useEffect(() => {
    if (isAuth) {
      api.getWishlist().then(items => {
        setWishlistGameIds(new Set(items.map(i => i.gameId)));
      }).catch(() => {});
    }
  }, [isAuth]);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 400);
    return () => clearTimeout(timer);
  }, [search]);

  const fetchGames = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getGames({ search: debouncedSearch, genreId, shopId, page, pageSize: 12 });
      setGames(res.items || []);
      setTotal(res.totalCount || 0);
    } catch (e) {
      message.error(e.message);
    }
    setLoading(false);
  }, [debouncedSearch, genreId, shopId, page]);

  useEffect(() => { fetchGames(); }, [fetchGames]);

  const toggleWishlist = async (e, gameId) => {
    e.stopPropagation();
    if (!isAuth) { message.warning('Please log in'); return; }
    try {
      const res = await api.toggleWishlist(gameId);
      message.success(res.message);
      setWishlistGameIds(prev => {
        const next = new Set(prev);
        if (res.added) next.add(gameId);
        else next.delete(gameId);
        return next;
      });
    } catch (e) { message.error(e.message); }
  };

  return (
    <div>
      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        <Col flex="auto">
          <Input prefix={<SearchOutlined />} placeholder="Search games..." size="large"
            value={search} onChange={e => setSearch(e.target.value)} allowClear />
        </Col>
        <Col>
          <Select placeholder="Genre" allowClear style={{ width: 160 }} size="large"
            value={genreId} onChange={v => { setGenreId(v); setPage(1); }}
            options={genres} />
        </Col>
        <Col>
          <Select placeholder="Shop" allowClear style={{ width: 160 }} size="large"
            value={shopId} onChange={v => { setShopId(v); setPage(1); }}
            options={[{ value: 1, label: 'Steam' }, { value: 2, label: 'GOG' }]} />
        </Col>
      </Row>

      <Spin spinning={loading}>
        <Row gutter={[16, 16]}>
          {games.map(g => (
            <Col xs={24} sm={12} md={8} lg={6} key={g.gameId}>
              <Badge.Ribbon text={g.max_discount > 0 ? `-${g.max_discount}%` : ''} color={g.max_discount >= 50 ? 'red' : 'blue'}
                style={{ display: g.max_discount > 0 ? 'block' : 'none' }}>
                <Card hoverable onClick={() => navigate(`/games/${g.gameId}`)}
                  actions={[
                    <Button type="text" 
                      icon={wishlistGameIds.has(g.gameId) ? <HeartFilled style={{ color: '#ff4d4f' }} /> : <HeartOutlined />}
                      onClick={e => toggleWishlist(e, g.gameId)} key="wish" />
                  ]}>
                  <Card.Meta
                    title={g.title}
                    description={
                      <div>
                        {g.developer_name && <div style={{ color: '#888', fontSize: 12 }}>{g.developer_name}</div>}
                        {g.genres && <div style={{ marginTop: 4 }}>{g.genres.split(', ').map(gen => <Tag key={gen} style={{ fontSize: 11 }}>{gen}</Tag>)}</div>}
                        <div style={{ marginTop: 8, fontSize: 18, fontWeight: 700 }}>
                          {g.min_price != null ? `$${Number(g.min_price).toFixed(2)}` : 'N/A'}
                        </div>
                        {g.available_in_shops > 0 && <div style={{ fontSize: 11, color: '#999' }}>{g.available_in_shops} shop(s)</div>}
                      </div>
                    }
                  />
                </Card>
              </Badge.Ribbon>
            </Col>
          ))}
        </Row>
      </Spin>

      {total > 12 && (
        <div style={{ textAlign: 'center', marginTop: 32 }}>
          <Pagination current={page} total={total} pageSize={12} onChange={p => setPage(p)} showSizeChanger={false} />
        </div>
      )}
    </div>
  );
}
