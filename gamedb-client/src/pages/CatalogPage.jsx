import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { message } from 'antd';
import { api } from '../api';
import { useAuth } from '../context/AuthContext';

/* ─── Constants ───────────────────────────────────────────────────── */
const GENRES = [
  { value: 1, label: 'RPG' },
  { value: 2, label: 'Action' },
  { value: 3, label: 'Adventure' },
  { value: 4, label: 'Roguelike' },
  { value: 5, label: 'Open World' },
  { value: 6, label: 'FPS' },
  { value: 7, label: 'Indie' },
  { value: 8, label: 'Strategy' },
];

const SHOPS = [
  { value: 1, label: 'Steam' },
  { value: 2, label: 'GOG' },
  { value: 3, label: 'Epic Games Store' },
];

const SORT_OPTIONS = [
  { value: 'relevance', label: 'Relevance' },
  { value: 'rating', label: 'Top Rated' },
  { value: 'price_asc', label: 'Price: Low → High' },
  { value: 'price_desc', label: 'Price: High → Low' },
  { value: 'discount', label: 'Biggest Discount' },
  { value: 'name', label: 'Name A–Z' },
  { value: 'newest', label: 'Newest First' },
];

const CONTENT_TYPE_OPTIONS = [
  { value: 'all', label: 'All' },
  { value: 'main_game', label: 'Main game' },
  { value: 'bundle', label: 'Bundle' },
  { value: 'dlc', label: 'DLCs' },
];

const DISCOUNT_PRESETS = [
  { value: 0, label: 'Any' },
  { value: 10, label: '10%+' },
  { value: 25, label: '25%+' },
  { value: 50, label: '50%+' },
  { value: 75, label: '75%+' },
];

const PRICE_PRESETS = [
  { value: '', label: 'Any' },
  { value: '5', label: 'Under $5' },
  { value: '10', label: 'Under $10' },
  { value: '20', label: 'Under $20' },
  { value: '40', label: 'Under $40' },
];

const PAGE_SIZE = 15;

/* ─── Cover placeholder ───────────────────────────────────────────── */
function coverHue(title = '') {
  return [...title].reduce((acc, c) => acc + c.charCodeAt(0), 0) % 360;
}

function extractSteamAppId(value) {
  if (!value) return null;
  const normalized = String(value).trim();
  if (/^\d+$/.test(normalized)) return normalized;
  const m = normalized.match(/\/app\/(\d+)(?:[/?#]|$)/i);
  return m ? m[1] : null;
}

function getSteamCoverUrls(steamAppId) {
  const appId = extractSteamAppId(steamAppId);
  if (!appId) return [];
  const base = `https://cdn.cloudflare.steamstatic.com/steam/apps/${appId}`;
  return [
    `${base}/header.jpg`,
    `${base}/capsule_231x87.jpg`,
    `${base}/capsule_184x69.jpg`,
  ];
}

function getCoverUrls(coverSource) {
  if (!coverSource) return [];
  const src = String(coverSource);
  // "steam:{appId}" — generate multiple CDN fallback URLs
  if (src.startsWith('steam:')) {
    const appId = src.slice(6);
    return getSteamCoverUrls(appId);
  }
  // Pure digits — treat as Steam AppId for backward compat
  if (/^\d+$/.test(src)) return getSteamCoverUrls(src);
  // Full URL (IGDB cover, RAWG image, etc.) — use directly
  if (/^https?:\/\//i.test(src)) return [src];
  // URL-like but no scheme — try as-is
  return [src];
}

/* ─── Sub-components ──────────────────────────────────────────────── */
function FilterLabel({ children }) {
  return (
    <div style={{
      fontSize: 10, fontWeight: 700, letterSpacing: '0.08em',
      color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: 6,
    }}>
      {children}
    </div>
  );
}

function FSelect({ value, onChange, options, placeholder }) {
  return (
    <select
      value={value ?? ''}
      onChange={e => onChange(e.target.value || null)}
      style={{
        width: '100%', padding: '7px 8px',
        background: 'var(--bg-input)', border: '1px solid var(--border)',
        color: value ? 'var(--text-primary)' : 'var(--text-muted)',
        borderRadius: 'var(--radius)', fontSize: 12, cursor: 'pointer',
        outline: 'none',
        backgroundColor: '#1f1f1f',
      }}
    >
      {placeholder && <option value="" style={{ color: '#8c8c8c', backgroundColor: '#1f1f1f' }}>{placeholder}</option>}
      {options.map(o => <option key={o.value} value={o.value} style={{ color: '#f5f5f5', backgroundColor: '#1f1f1f' }}>{o.label}</option>)}
    </select>
  );
}

function DiscountPicker({ value, onChange }) {
  return (
    <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
      {DISCOUNT_PRESETS.map(p => {
        const active = value === p.value;
        return (
          <button key={p.value} onClick={() => onChange(active ? 0 : p.value)} style={{
            padding: '4px 8px', borderRadius: 'var(--radius)',
            background: active ? 'var(--green)' : 'var(--bg-input)',
            border: '1px solid ' + (active ? 'var(--green)' : 'var(--border)'),
            color: active ? '#fff' : 'var(--text-secondary)',
            fontSize: 11, cursor: 'pointer', fontWeight: active ? 700 : 400,
          }}>{p.label}</button>
        );
      })}
    </div>
  );
}

function PricePicker({ value, onChange }) {
  return (
    <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
      {PRICE_PRESETS.map(p => {
        const active = value === p.value;
        return (
          <button key={p.value} onClick={() => onChange(active ? '' : p.value)} style={{
            padding: '4px 8px', borderRadius: 'var(--radius)',
            background: active ? 'var(--blue)' : 'var(--bg-input)',
            border: '1px solid ' + (active ? 'var(--blue)' : 'var(--border)'),
            color: active ? '#fff' : 'var(--text-secondary)',
            fontSize: 11, cursor: 'pointer', fontWeight: active ? 700 : 400,
          }}>{p.label}</button>
        );
      })}
    </div>
  );
}

function HeartIcon({ filled }) {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24"
      fill={filled ? 'var(--red)' : 'none'}
      stroke={filled ? 'var(--red)' : 'currentColor'} strokeWidth="2">
      <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
    </svg>
  );
}

function GameRow({ game, inWishlist, onWishlist, onClick, coverUrls }) {
  const [hov, setHov] = useState(false);
  const [coverFailed, setCoverFailed] = useState(false);
  const [coverIndex, setCoverIndex] = useState(0);
  const genres = game.genres ? game.genres.split(', ') : [];
  const hue = coverHue(game.title);
  const hasDiscount = game.max_discount > 0;
  const currentCoverUrl = coverUrls?.[coverIndex] || null;

  useEffect(() => {
    setCoverFailed(false);
    setCoverIndex(0);
  }, [game.gameId, coverUrls]);

  return (
    <div
      onClick={onClick}
      onMouseEnter={() => setHov(true)}
      onMouseLeave={() => setHov(false)}
      style={{
        display: 'flex', alignItems: 'center', gap: 14,
        padding: '10px 14px',
        background: hov ? 'var(--bg-hover)' : 'var(--bg-card)',
        border: '1px solid var(--border)',
        borderRadius: 'var(--radius-lg)',
        cursor: 'pointer',
        transition: 'background 0.1s',
      }}
    >
      {/* Cover */}
      {!coverFailed && currentCoverUrl ? (
        <img
          src={currentCoverUrl}
          alt={game.title}
          loading="lazy"
          referrerPolicy="no-referrer"
          onError={() => {
            if (Array.isArray(coverUrls) && coverIndex < coverUrls.length - 1) {
              setCoverIndex(i => i + 1);
              return;
            }
            setCoverFailed(true);
          }}
          style={{
            width: 52, height: 70, flexShrink: 0, objectFit: 'cover',
            borderRadius: 'var(--radius)', border: '1px solid var(--border)',
          }}
        />
      ) : (
        <div style={{
          width: 52, height: 70, flexShrink: 0,
          borderRadius: 'var(--radius)',
          background: `hsl(${hue},28%,22%)`,
          border: '1px solid var(--border)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 20, fontWeight: 800, color: 'rgba(255,255,255,0.5)',
          fontFamily: 'var(--font-display)',
        }}>
          {(game.title || '?')[0].toUpperCase()}
        </div>
      )}

      {/* Meta */}
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          fontWeight: 600, fontSize: 14, marginBottom: 2,
          whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
        }}>
          {game.title}
        </div>
        {game.developer_name && (
          <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginBottom: 5 }}>
            {game.developer_name}
          </div>
        )}
        <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
          {genres.slice(0, 3).map(g => (
            <span key={g} style={{
              fontSize: 10, padding: '2px 6px',
              background: 'var(--bg-active)', border: '1px solid var(--border)',
              borderRadius: 'var(--radius)', color: 'var(--text-muted)',
            }}>{g}</span>
          ))}
          {game.rating > 0 && (
            <span style={{
              fontSize: 10, padding: '2px 6px',
              background: 'rgba(24,144,255,0.15)', border: '1px solid rgba(24,144,255,0.3)',
              borderRadius: 'var(--radius)', color: '#1890ff',
            }}>
              ★ {Math.round(game.rating)}/100
            </span>
          )}
          {game.is_dlc && (
            <span style={{
              fontSize: 10, padding: '2px 6px',
              background: 'rgba(245, 34, 45, 0.15)', border: '1px solid rgba(245, 34, 45, 0.35)',
              borderRadius: 'var(--radius)', color: '#f5222d',
            }}>
              DLC
            </span>
          )}
          {game.available_in_shops > 0 && (
            <span style={{
              fontSize: 10, padding: '2px 6px',
              background: 'var(--green-dim)', border: '1px solid rgba(61,191,80,0.2)',
              borderRadius: 'var(--radius)', color: 'var(--green)',
            }}>
              {game.available_in_shops} shop{game.available_in_shops > 1 ? 's' : ''}
            </span>
          )}
        </div>
      </div>

      {/* Price */}
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 3, flexShrink: 0 }}>
        {hasDiscount && (
          <span style={{
            fontSize: 10, fontWeight: 700, padding: '2px 6px',
            background: game.max_discount >= 50 ? 'var(--red)' : 'var(--green)',
            color: '#fff', borderRadius: 'var(--radius)',
            fontFamily: 'var(--font-mono)',
          }}>-{game.max_discount}%</span>
        )}
        <span style={{
          fontFamily: 'var(--font-mono)', fontWeight: 600, fontSize: 15,
          color: hasDiscount ? 'var(--green)' : 'var(--text-primary)',
        }}>
          {game.min_price != null ? `$${Number(game.min_price).toFixed(2)}` : 'N/A'}
        </span>
      </div>

      {/* Wishlist */}
      <button
        onClick={e => { e.stopPropagation(); onWishlist(); }}
        style={{
          background: 'transparent', border: 'none', cursor: 'pointer',
          padding: 6, borderRadius: 'var(--radius)', flexShrink: 0,
          color: inWishlist ? 'var(--red)' : 'var(--text-muted)',
          display: 'flex', alignItems: 'center',
        }}
        onMouseEnter={e => e.currentTarget.style.color = 'var(--red)'}
        onMouseLeave={e => e.currentTarget.style.color = inWishlist ? 'var(--red)' : 'var(--text-muted)'}
      >
        <HeartIcon filled={inWishlist} />
      </button>
    </div>
  );
}

/* ─── Main Page ───────────────────────────────────────────────────── */
export default function CatalogPage() {
  const [games, setGames] = useState([]);
  const [coverUrls, setCoverUrls] = useState({});
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [wishlistIds, setWishlistIds] = useState(new Set());

  // Filters
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

  useEffect(() => {
    let cancelled = false;
    const pending = games.filter(g => coverUrls[g.gameId] === undefined).map(g => g.gameId);
    if (pending.length === 0) return;

    (async () => {
      let coversByGameId = {};
      try {
        coversByGameId = await api.getGameCovers(pending);
      } catch {}

      if (cancelled) return;
      setCoverUrls(prev => {
        const next = { ...prev };
        for (const gameId of pending) {
          if (next[gameId] === undefined) next[gameId] = null;
        }
        for (const [gameId, coverSource] of Object.entries(coversByGameId || {})) {
          const urls = getCoverUrls(coverSource);
          next[Number(gameId)] = urls.length > 0 ? urls : null;
        }
        return next;
      });
    })();

    return () => { cancelled = true; };
  }, [games, coverUrls]);

  const toggleWishlist = async (gameId) => {
    if (!isAuth) { message.warning('Please log in'); return; }
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

  /* ── Sidebar ── */
  const sidebar = (
    <aside style={{ width: 210, flexShrink: 0, position: 'sticky', top: 'calc(var(--nav-height) + 24px)' }}>
      <div style={{
        background: 'var(--bg-card)', border: '1px solid var(--border)',
        borderRadius: 'var(--radius-lg)', padding: '14px 14px',
        display: 'flex', flexDirection: 'column', gap: 14,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', letterSpacing: '0.08em', textTransform: 'uppercase' }}>
            Filters
          </span>
          {hasFilters && (
            <button onClick={clearFilters} style={{
              background: 'none', border: 'none', color: 'var(--red)',
              fontSize: 11, cursor: 'pointer', padding: 0,
            }}>Clear all</button>
          )}
        </div>

        {/* Sort */}
        <div>
          <FilterLabel>Sort by</FilterLabel>
          <FSelect
            value={sortBy}
            onChange={v => { setSortBy(v || 'relevance'); setPage(1); }}
            options={SORT_OPTIONS}
          />
        </div>

        <div>
          <FilterLabel>Content type</FilterLabel>
          <FSelect
            value={contentType}
            onChange={v => { setContentType(v || 'all'); setPage(1); }}
            options={CONTENT_TYPE_OPTIONS}
          />
        </div>

        {/* Genre */}
        <div>
          <FilterLabel>Genre</FilterLabel>
          <FSelect
            value={genreId}
            onChange={v => { setGenreId(v ? Number(v) : null); setPage(1); }}
            options={GENRES}
            placeholder="All genres"
          />
        </div>

        {/* Shop */}
        <div>
          <FilterLabel>Store</FilterLabel>
          <FSelect
            value={shopId}
            onChange={v => { setShopId(v ? Number(v) : null); setPage(1); }}
            options={SHOPS}
            placeholder="All stores"
          />
        </div>

        {/* Price */}
        <div>
          <FilterLabel>Max price</FilterLabel>
          <PricePicker value={maxPrice} onChange={v => { setMaxPrice(v); setPage(1); }} />
        </div>

        {/* Discount */}
        <div>
          <FilterLabel>Min discount</FilterLabel>
          <DiscountPicker value={minDiscount} onChange={v => { setMinDiscount(v); setPage(1); }} />
        </div>

        {/* On sale toggle */}
        <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
          <div
            onClick={() => { setOnSaleOnly(v => !v); setPage(1); }}
            style={{
              width: 32, height: 18, borderRadius: 9,
              background: onSaleOnly ? 'var(--green)' : 'var(--bg-input)',
              border: '1px solid ' + (onSaleOnly ? 'var(--green)' : 'var(--border)'),
              position: 'relative', cursor: 'pointer', flexShrink: 0,
              transition: 'background 0.2s',
            }}
          >
            <div style={{
              position: 'absolute', top: 2,
              left: onSaleOnly ? 14 : 2,
              width: 12, height: 12,
              borderRadius: '50%', background: '#fff',
              transition: 'left 0.2s',
            }} />
          </div>
          <span style={{ fontSize: 12, color: 'var(--text-secondary)', userSelect: 'none' }}>
            On sale only
          </span>
        </label>
      </div>
    </aside>
  );

  /* ── Render ── */
  return (
    <div style={{ display: 'flex', gap: 20, alignItems: 'flex-start' }}>
      {sidebar}

      <div style={{ flex: 1, minWidth: 0 }}>
        {/* Search */}
        <div style={{ position: 'relative', marginBottom: 14 }}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
            style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)', pointerEvents: 'none' }}>
            <circle cx="11" cy="11" r="8" /><line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Search games..."
            style={{
              width: '100%', padding: '9px 12px 9px 34px',
              background: 'var(--bg-input)', border: '1px solid var(--border)',
              borderRadius: 'var(--radius-lg)', color: 'var(--text-primary)',
              fontSize: 14, outline: 'none',
            }}
            onFocus={e => e.target.style.borderColor = 'var(--border-hover)'}
            onBlur={e => e.target.style.borderColor = 'var(--border)'}
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

        {/* Count */}
        <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 10 }}>
          {loading ? 'Loading...' : `${total.toLocaleString()} game${total !== 1 ? 's' : ''}`}
        </div>

        {/* List */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 5, opacity: loading ? 0.5 : 1, transition: 'opacity 0.15s' }}>
          {games.map(g => (
            <GameRow
              key={g.gameId}
              game={g}
              coverUrls={coverUrls[g.gameId]}
              inWishlist={wishlistIds.has(g.gameId)}
              onWishlist={() => toggleWishlist(g.gameId)}
              onClick={() => navigate(`/games/${g.gameId}`)}
            />
          ))}
          {!loading && games.length === 0 && (
            <div style={{ textAlign: 'center', padding: '60px 0', color: 'var(--text-muted)', fontSize: 14 }}>
              No games found
            </div>
          )}
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div style={{ display: 'flex', justifyContent: 'center', gap: 4, marginTop: 24 }}>
            <PageBtn label="‹" disabled={page === 1} onClick={() => setPage(p => p - 1)} />
            {paginationRange(page, totalPages).map((p, i) =>
              p === '…'
                ? <span key={`dots-${i}`} style={{ padding: '0 4px', color: 'var(--text-muted)', lineHeight: '32px' }}>…</span>
                : <PageBtn key={p} label={p} active={p === page} onClick={() => setPage(p)} />
            )}
            <PageBtn label="›" disabled={page === totalPages} onClick={() => setPage(p => p + 1)} />
          </div>
        )}
      </div>
    </div>
  );
}

/* ─── Small helpers ───────────────────────────────────────────────── */
function Chip({ label, onRemove, color }) {
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 5,
      padding: '3px 8px', borderRadius: 20,
      background: color ? `${color}22` : 'var(--bg-active)',
      border: `1px solid ${color || 'var(--border)'}`,
      fontSize: 11, color: color || 'var(--text-secondary)',
    }}>
      {label}
      <span onClick={onRemove} style={{ cursor: 'pointer', opacity: 0.7, fontWeight: 700 }}>×</span>
    </span>
  );
}

function PageBtn({ label, active, disabled, onClick }) {
  return (
    <button onClick={onClick} disabled={disabled} style={{
      minWidth: 32, height: 32, padding: '0 6px',
      borderRadius: 'var(--radius)',
      background: active ? 'var(--green)' : 'var(--bg-card)',
      border: '1px solid ' + (active ? 'var(--green)' : 'var(--border)'),
      color: active ? '#fff' : disabled ? 'var(--text-muted)' : 'var(--text-secondary)',
      cursor: disabled ? 'default' : 'pointer',
      fontSize: 13,
    }}>{label}</button>
  );
}

function paginationRange(current, total) {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  if (current <= 4) return [1, 2, 3, 4, 5, '…', total];
  if (current >= total - 3) return [1, '…', total - 4, total - 3, total - 2, total - 1, total];
  return [1, '…', current - 1, current, current + 1, '…', total];
}
