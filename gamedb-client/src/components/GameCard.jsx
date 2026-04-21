import { useState } from 'react';

function coverHue(title = '') {
  return [...title].reduce((acc, c) => acc + c.charCodeAt(0), 0) % 360;
}

function HeartIcon({ filled }) {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24"
      fill={filled ? 'var(--red)' : 'none'}
      stroke={filled ? 'var(--red)' : 'currentColor'} strokeWidth="2">
      <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
    </svg>
  );
}

export default function GameCard({ game, inWishlist, onWishlist, onClick }) {
  const [hov, setHov] = useState(false);
  const [wishHov, setWishHov] = useState(false);
  const genres = game.genres ? game.genres.split(', ') : [];
  const hue = coverHue(game.title);
  const hasDiscount = game.max_discount > 0;

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={onClick}
      onKeyDown={e => e.key === 'Enter' && onClick?.()}
      onMouseEnter={() => setHov(true)}
      onMouseLeave={() => setHov(false)}
      style={{
        display: 'flex', alignItems: 'center', gap: 14,
        padding: '12px 16px',
        background: hov ? 'var(--bg-hover)' : 'var(--bg-card)',
        border: `1px solid ${hov ? 'var(--border-hover)' : 'var(--border)'}`,
        borderRadius: 'var(--radius-lg)',
        cursor: 'pointer',
        transition: 'background var(--transition-fast), border-color var(--transition-fast), transform var(--transition-fast), box-shadow var(--transition-fast)',
        transform: hov ? 'translateY(-1px)' : 'none',
        boxShadow: hov ? 'var(--shadow-sm)' : 'none',
        outline: 'none',
      }}
    >
      {/* Cover thumbnail */}
      <div style={{
        width: 54, height: 72, flexShrink: 0,
        borderRadius: 'var(--radius)',
        background: `linear-gradient(145deg, hsl(${hue},30%,22%), hsl(${(hue + 40) % 360},20%,16%))`,
        border: '1px solid var(--border)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 22, fontWeight: 800, color: `hsl(${hue},60%,70%)`,
        fontFamily: 'var(--font-display)',
        letterSpacing: '-0.02em',
        boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.06)',
      }}>
        {(game.title || '?')[0].toUpperCase()}
      </div>

      {/* Meta */}
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          fontWeight: 600, fontSize: 14, marginBottom: 2,
          whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
          color: 'var(--text-primary)',
        }}>
          {game.title}
        </div>
        {game.developer_name && (
          <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 6 }}>
            {game.developer_name}
          </div>
        )}
        <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
          {genres.slice(0, 3).map(g => (
            <span key={g} style={{
              fontSize: 10, padding: '2px 6px',
              background: 'var(--bg-active)', border: '1px solid var(--border)',
              borderRadius: 'var(--radius)', color: 'var(--text-muted)',
              fontWeight: 500,
            }}>{g}</span>
          ))}
          {game.rating > 0 && (
            <span style={{
              fontSize: 10, padding: '2px 6px',
              background: 'rgba(79,156,249,0.12)', border: '1px solid rgba(79,156,249,0.25)',
              borderRadius: 'var(--radius)', color: 'var(--primary)',
              fontWeight: 600,
            }}>
              ★ {Math.round(game.rating)}/100
            </span>
          )}
          {game.is_dlc && (
            <span style={{
              fontSize: 10, padding: '2px 6px',
              background: 'var(--red-dim)', border: '1px solid rgba(245,34,45,0.3)',
              borderRadius: 'var(--radius)', color: 'var(--red)',
              fontWeight: 600,
            }}>DLC</span>
          )}
          {game.available_in_shops > 0 && (
            <span style={{
              fontSize: 10, padding: '2px 6px',
              background: 'var(--green-dim)', border: '1px solid rgba(61,191,80,0.2)',
              borderRadius: 'var(--radius)', color: 'var(--green)',
              fontWeight: 600,
            }}>
              {game.available_in_shops} shop{game.available_in_shops > 1 ? 's' : ''}
            </span>
          )}
        </div>
      </div>

      {/* Price column */}
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 3, flexShrink: 0, minWidth: 68 }}>
        {hasDiscount && (
          <span style={{
            fontSize: 10, fontWeight: 700, padding: '2px 7px',
            background: game.max_discount >= 50 ? 'var(--red)' : 'var(--green)',
            color: '#fff', borderRadius: 20,
            fontFamily: 'var(--font-mono)',
            letterSpacing: '0.02em',
          }}>-{game.max_discount}%</span>
        )}
        <span style={{
          fontFamily: 'var(--font-mono)', fontWeight: 700, fontSize: 15,
          color: hasDiscount ? 'var(--green)' : 'var(--text-primary)',
        }}>
          {game.min_price != null ? `$${Number(game.min_price).toFixed(2)}` : 'N/A'}
        </span>
      </div>

      {/* Wishlist button */}
      <button
        aria-label={inWishlist ? 'Remove from wishlist' : 'Add to wishlist'}
        onClick={e => { e.stopPropagation(); onWishlist?.(); }}
        onMouseEnter={() => setWishHov(true)}
        onMouseLeave={() => setWishHov(false)}
        style={{
          background: inWishlist || wishHov ? 'var(--red-dim)' : 'transparent',
          border: `1px solid ${inWishlist || wishHov ? 'rgba(245,34,45,0.4)' : 'var(--border)'}`,
          cursor: 'pointer', padding: 7, borderRadius: 'var(--radius)', flexShrink: 0,
          color: inWishlist || wishHov ? 'var(--red)' : 'var(--text-muted)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          transition: 'all var(--transition-fast)',
        }}
      >
        <HeartIcon filled={inWishlist} />
      </button>
    </div>
  );
}
