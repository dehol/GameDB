import { useState } from 'react';

/* ─── FilterPanel ─────────────────────────────────────────────────── */
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
  { value: 'games', label: 'Games only' },
  { value: 'dlc', label: 'DLC only' },
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
  { value: '5', label: '<$5' },
  { value: '10', label: '<$10' },
  { value: '20', label: '<$20' },
  { value: '40', label: '<$40' },
];

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
  const [focused, setFocused] = useState(false);
  return (
    <select
      value={value ?? ''}
      onChange={e => onChange(e.target.value || null)}
      onFocus={() => setFocused(true)}
      onBlur={() => setFocused(false)}
      style={{
        width: '100%', padding: '7px 10px',
        background: 'var(--bg-input)',
        border: `1px solid ${focused ? 'var(--primary)' : 'var(--border)'}`,
        boxShadow: focused ? '0 0 0 2px rgba(79,156,249,0.15)' : 'none',
        color: value ? 'var(--text-primary)' : 'var(--text-muted)',
        borderRadius: 'var(--radius)', fontSize: 12, cursor: 'pointer',
        outline: 'none', appearance: 'none',
        backgroundImage: `url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='10' height='6' viewBox='0 0 10 6'%3E%3Cpath d='M0 0l5 6 5-6z' fill='%235a5a78'/%3E%3C/svg%3E")`,
        backgroundRepeat: 'no-repeat', backgroundPosition: 'right 8px center',
        paddingRight: 24, transition: 'border-color var(--transition-fast), box-shadow var(--transition-fast)',
      }}
    >
      {placeholder && <option value="" style={{ color: 'var(--text-muted)' }}>{placeholder}</option>}
      {options.map(o => <option key={o.value} value={o.value} style={{ color: 'var(--text-primary)', background: 'var(--bg-elevated)' }}>{o.label}</option>)}
    </select>
  );
}

function PresetPicker({ presets, value, onChange, accentVar = '--green' }) {
  return (
    <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
      {presets.map(p => {
        const active = String(value) === String(p.value);
        return (
          <button key={String(p.value)} onClick={() => onChange(active ? (typeof p.value === 'number' ? 0 : '') : p.value)} style={{
            padding: '3px 8px', borderRadius: 20,
            background: active ? `var(${accentVar})` : 'var(--bg-input)',
            border: `1px solid ${active ? `var(${accentVar})` : 'var(--border)'}`,
            color: active ? '#fff' : 'var(--text-secondary)',
            fontSize: 11, cursor: 'pointer', fontWeight: active ? 700 : 400,
            transition: 'all var(--transition-fast)',
          }}>{p.label}</button>
        );
      })}
    </div>
  );
}

export default function FilterPanel({
  sortBy, setSortBy,
  contentType, setContentType,
  genreId, setGenreId,
  shopId, setShopId,
  maxPrice, setMaxPrice,
  minDiscount, setMinDiscount,
  onSaleOnly, setOnSaleOnly,
  hasFilters, onClear,
  onFilterChange,
}) {
  const wrap = (setter) => (v) => { setter(v); onFilterChange?.(); };

  return (
    <aside style={{ width: 210, flexShrink: 0, position: 'sticky', top: 'calc(var(--nav-height) + 24px)' }}>
      <div style={{
        background: 'var(--bg-card)', border: '1px solid var(--border)',
        borderRadius: 'var(--radius-lg)', padding: '16px 14px',
        display: 'flex', flexDirection: 'column', gap: 16,
      }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', letterSpacing: '0.08em', textTransform: 'uppercase' }}>
            Filters
          </span>
          {hasFilters && (
            <button onClick={onClear} style={{
              background: 'none', border: 'none', color: 'var(--red)',
              fontSize: 11, cursor: 'pointer', padding: 0, fontWeight: 600,
              transition: 'opacity var(--transition-fast)',
            }}
              onMouseEnter={e => e.currentTarget.style.opacity = '0.75'}
              onMouseLeave={e => e.currentTarget.style.opacity = '1'}
            >
              Clear all
            </button>
          )}
        </div>

        <div><FilterLabel>Sort by</FilterLabel>
          <FSelect value={sortBy} onChange={v => wrap(setSortBy)(v || 'relevance')} options={SORT_OPTIONS} />
        </div>

        <div><FilterLabel>Content type</FilterLabel>
          <FSelect value={contentType} onChange={v => wrap(setContentType)(v || 'all')} options={CONTENT_TYPE_OPTIONS} />
        </div>

        <div><FilterLabel>Genre</FilterLabel>
          <FSelect value={genreId} onChange={v => wrap(setGenreId)(v ? Number(v) : null)} options={GENRES} placeholder="All genres" />
        </div>

        <div><FilterLabel>Store</FilterLabel>
          <FSelect value={shopId} onChange={v => wrap(setShopId)(v ? Number(v) : null)} options={SHOPS} placeholder="All stores" />
        </div>

        <div><FilterLabel>Max price</FilterLabel>
          <PresetPicker presets={PRICE_PRESETS} value={maxPrice} onChange={v => wrap(setMaxPrice)(v)} accentVar="--blue" />
        </div>

        <div><FilterLabel>Min discount</FilterLabel>
          <PresetPicker presets={DISCOUNT_PRESETS} value={minDiscount} onChange={v => wrap(setMinDiscount)(v)} accentVar="--green" />
        </div>

        {/* On sale toggle */}
        <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
          <div
            role="switch"
            aria-checked={onSaleOnly}
            tabIndex={0}
            onClick={() => wrap(setOnSaleOnly)(v => !v)}
            onKeyDown={e => e.key === 'Enter' && wrap(setOnSaleOnly)(v => !v)}
            style={{
              width: 32, height: 18, borderRadius: 9,
              background: onSaleOnly ? 'var(--green)' : 'var(--bg-input)',
              border: `1px solid ${onSaleOnly ? 'var(--green)' : 'var(--border)'}`,
              position: 'relative', cursor: 'pointer', flexShrink: 0,
              transition: 'background var(--transition), border-color var(--transition)',
              outline: 'none',
            }}
          >
            <div style={{
              position: 'absolute', top: 2,
              left: onSaleOnly ? 14 : 2,
              width: 12, height: 12,
              borderRadius: '50%', background: '#fff',
              transition: 'left var(--transition)',
              boxShadow: '0 1px 3px rgba(0,0,0,0.3)',
            }} />
          </div>
          <span style={{ fontSize: 12, color: 'var(--text-secondary)', userSelect: 'none', fontWeight: 500 }}>
            On sale only
          </span>
        </label>
      </div>
    </aside>
  );
}

export { GENRES, SHOPS, SORT_OPTIONS, CONTENT_TYPE_OPTIONS, DISCOUNT_PRESETS, PRICE_PRESETS };
