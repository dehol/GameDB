import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { message } from 'antd';
import { api } from '../api';
import { useAuth } from '../context/AuthContext';
import GameCard from '../components/GameCard';
import FilterPanel, { SORT_OPTIONS, CONTENT_TYPE_OPTIONS, GENRES, SHOPS } from '../components/FilterPanel';
import EmptyState from '../components/EmptyState';
import { SearchOutlined, AppstoreOutlined } from '@ant-design/icons';

const PAGE_SIZE = 15;

/* ─── Skeleton row ────────────────────────────────────────────────── */
function SkeletonRow() {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 14,
      padding: '12px 16px',
      background: 'var(--bg-card)', border: '1px solid var(--border)',
      borderRadius: 'var(--radius-lg)',
    }}>
      <div className="skeleton" style={{ width: 54, height: 72, borderRadius: 'var(--radius)', flexShrink: 0 }} />
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div className="skeleton" style={{ height: 14, width: '55%', borderRadius: 4 }} />
        <div className="skeleton" style={{ height: 10, width: '30%', borderRadius: 4 }} />
        <div style={{ display: 'flex', gap: 4 }}>
          <div className="skeleton" style={{ height: 18, width: 44, borderRadius: 4 }} />
          <div className="skeleton" style={{ height: 18, width: 44, borderRadius: 4 }} />
        </div>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 6 }}>
        <div className="skeleton" style={{ height: 18, width: 36, borderRadius: 10 }} />
        <div className="skeleton" style={{ height: 20, width: 56, borderRadius: 4 }} />
      </div>
      <div className="skeleton" style={{ width: 30, height: 30, borderRadius: 'var(--radius)', flexShrink: 0 }} />
    </div>
  );
}

/* ─── Chip ──────────────────────────────────────────────────────────── */
function Chip({ label, onRemove, color }) {
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 5,
      padding: '3px 8px', borderRadius: 20,
      background: color ? `${color}22` : 'var(--bg-active)',
      border: `1px solid ${color || 'var(--border)'}`,
      fontSize: 11, color: color || 'var(--text-secondary)',
      fontWeight: 500,
    }}>
      {label}
      <span
        role="button"
        tabIndex={0}
        onClick={onRemove}
        onKeyDown={e => e.key === 'Enter' && onRemove()}
        style={{ cursor: 'pointer', opacity: 0.7, fontWeight: 700, lineHeight: 1 }}
      >×</span>
    </span>
  );
}

/* ─── Pagination ────────────────────────────────────────────────────── */
function PageBtn({ label, active, disabled, onClick }) {
  return (
    <button onClick={onClick} disabled={disabled} style={{
      minWidth: 32, height: 32, padding: '0 6px',
      borderRadius: 'var(--radius)',
      background: active ? 'var(--primary)' : 'var(--bg-card)',
      border: `1px solid ${active ? 'var(--primary)' : 'var(--border)'}`,
      color: active ? '#fff' : disabled ? 'var(--text-muted)' : 'var(--text-secondary)',
      cursor: disabled ? 'default' : 'pointer',
      fontSize: 13, fontWeight: active ? 700 : 400,
      transition: 'all var(--transition-fast)',
      boxShadow: active ? '0 2px 8px rgba(79,156,249,0.3)' : 'none',
    }}>{label}</button>
  );
}

function paginationRange(current, total) {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  if (current <= 4) return [1, 2, 3, 4, 5, '…', total];
  if (current >= total - 3) return [1, '…', total - 4, total - 3, total - 2, total - 1, total];
  return [1, '…', current - 1, current, current + 1, '…', total];
}

/* ─── Main Page ─────────────────────────────────────────────────────── */
export default function CatalogPage() {
  const [games, setGames] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [wishlistIds, setWishlistIds] = useState(new Set());

  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [genreId, setGenreId] = useState(null);
  const [shopId, setShopId] = useState(null);
  const [maxPrice, setMaxPrice] = useState('');
  const [minDiscount, setMinDiscount] = useState(0);
  const [onSaleOnly, setOnSaleOnly] = useState(false);
  const [sortBy, setSortBy] = useState('relevance');
  const [contentType, setContentType] = useState('all');
  const [page, setPage] = useState(1);

  const navigate = useNavigate();
  const { isAuth } = useAuth();

  const hasFilters = genreId || shopId || maxPrice || minDiscount > 0 || onSaleOnly || sortBy !== 'relevance' || contentType !== 'all';

  useEffect(() => {
    if (isAuth) {
      api.getWishlist()
        .then(items => setWishlistIds(new Set(items.map(i => i.gameId))))
        .catch(() => {});
    }
  }, [isAuth]);

  useEffect(() => {
    const t = setTimeout(() => { setDebouncedSearch(search); setPage(1); }, 400);
    return () => clearTimeout(t);
  }, [search]);

  const fetchGames = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getGames({
        search: debouncedSearch,
        genreId,
        shopId,
        maxPrice: maxPrice || undefined,
        minDiscount: minDiscount > 0 ? minDiscount : undefined,
        onSaleOnly: onSaleOnly || undefined,
        sortBy: sortBy !== 'relevance' ? sortBy : undefined,
        contentType: contentType !== 'all' ? contentType : undefined,
        page,
        pageSize: PAGE_SIZE,
      });
      setGames(res.items || []);
      setTotal(res.totalCount || 0);
    } catch (e) {
      message.error(e.message);
    }
    setLoading(false);
  }, [debouncedSearch, genreId, shopId, maxPrice, minDiscount, onSaleOnly, sortBy, contentType, page]);

  useEffect(() => { fetchGames(); }, [fetchGames]);

  const toggleWishlist = async (gameId) => {
    if (!isAuth) { message.warning('Please log in to use the wishlist'); return; }
    try {
      const res = await api.toggleWishlist(gameId);
      setWishlistIds(prev => {
        const next = new Set(prev);
        if (res.added) next.add(gameId); else next.delete(gameId);
        return next;
      });
    } catch (e) { message.error(e.message); }
  };

  const clearFilters = () => {
    setGenreId(null); setShopId(null); setMaxPrice('');
    setMinDiscount(0); setOnSaleOnly(false); setSortBy('relevance'); setContentType('all'); setPage(1);
  };

  const totalPages = Math.ceil(total / PAGE_SIZE);

  return (
    <div style={{ display: 'flex', gap: 20, alignItems: 'flex-start' }} className="page-enter">
      {/* Sidebar filters */}
      <FilterPanel
        sortBy={sortBy} setSortBy={setSortBy}
        contentType={contentType} setContentType={setContentType}
        genreId={genreId} setGenreId={setGenreId}
        shopId={shopId} setShopId={setShopId}
        maxPrice={maxPrice} setMaxPrice={setMaxPrice}
        minDiscount={minDiscount} setMinDiscount={setMinDiscount}
        onSaleOnly={onSaleOnly} setOnSaleOnly={setOnSaleOnly}
        hasFilters={hasFilters} onClear={clearFilters}
        onFilterChange={() => setPage(1)}
      />

      {/* Main content */}
      <div style={{ flex: 1, minWidth: 0 }}>
        {/* Search bar */}
        <div style={{ position: 'relative', marginBottom: 14 }}>
          <SearchOutlined style={{
            position: 'absolute', left: 13, top: '50%', transform: 'translateY(-50%)',
            color: 'var(--text-muted)', pointerEvents: 'none', fontSize: 14,
          }} />
          <input
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Search games, developers..."
            style={{
              width: '100%', padding: '10px 12px 10px 36px',
              background: 'var(--bg-card)', border: '1px solid var(--border)',
              borderRadius: 'var(--radius-lg)', color: 'var(--text-primary)',
              fontSize: 14, outline: 'none',
              transition: 'border-color var(--transition-fast), box-shadow var(--transition-fast)',
            }}
            onFocus={e => {
              e.target.style.borderColor = 'var(--border-hover)';
              e.target.style.boxShadow = '0 0 0 2px rgba(79,156,249,0.15)';
            }}
            onBlur={e => {
              e.target.style.borderColor = 'var(--border)';
              e.target.style.boxShadow = 'none';
            }}
          />
        </div>

        {/* Active filter chips */}
        {hasFilters && (
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 12 }}>
            {sortBy !== 'relevance' && (
              <Chip label={`Sort: ${SORT_OPTIONS.find(o => o.value === sortBy)?.label}`} onRemove={() => setSortBy('relevance')} />
            )}
            {contentType !== 'all' && (
              <Chip label={`Type: ${CONTENT_TYPE_OPTIONS.find(o => o.value === contentType)?.label}`} onRemove={() => setContentType('all')} />
            )}
            {genreId && (
              <Chip label={GENRES.find(g => g.value === genreId)?.label} onRemove={() => setGenreId(null)} />
            )}
            {shopId && (
              <Chip label={SHOPS.find(s => s.value === shopId)?.label} onRemove={() => setShopId(null)} />
            )}
            {maxPrice && (
              <Chip label={`Under $${maxPrice}`} onRemove={() => setMaxPrice('')} />
            )}
            {minDiscount > 0 && (
              <Chip label={`${minDiscount}%+ off`} onRemove={() => setMinDiscount(0)} />
            )}
            {onSaleOnly && (
              <Chip label="On sale" onRemove={() => setOnSaleOnly(false)} color="var(--green)" />
            )}
          </div>
        )}

        {/* Count bar */}
        <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 10, height: 18 }}>
          {!loading && `${total.toLocaleString()} game${total !== 1 ? 's' : ''}`}
        </div>

        {/* Game list */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {loading
            ? Array.from({ length: 8 }).map((_, i) => <SkeletonRow key={i} />)
            : games.map(g => (
              <GameCard
                key={g.gameId}
                game={g}
                inWishlist={wishlistIds.has(g.gameId)}
                onWishlist={() => toggleWishlist(g.gameId)}
                onClick={() => navigate(`/games/${g.gameId}`)}
              />
            ))
          }

          {!loading && games.length === 0 && (
            <EmptyState
              icon={<AppstoreOutlined />}
              title="No games found"
              description={hasFilters
                ? 'Try adjusting your filters or search query to find what you\'re looking for.'
                : 'The catalog appears to be empty. Check back later.'}
              action={hasFilters ? clearFilters : undefined}
              actionLabel={hasFilters ? 'Clear filters' : undefined}
            />
          )}
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div style={{ display: 'flex', justifyContent: 'center', gap: 4, marginTop: 24, flexWrap: 'wrap' }}>
            <PageBtn label="‹" disabled={page === 1} onClick={() => setPage(p => p - 1)} />
            {paginationRange(page, totalPages).map((p, i) =>
              p === '…'
                ? <span key={`ellipsis-${i}`} style={{ padding: '0 4px', color: 'var(--text-muted)', lineHeight: '32px' }}>…</span>
                : <PageBtn key={p} label={p} active={p === page} onClick={() => setPage(p)} />
            )}
            <PageBtn label="›" disabled={page === totalPages} onClick={() => setPage(p => p + 1)} />
          </div>
        )}
      </div>
    </div>
  );
}
