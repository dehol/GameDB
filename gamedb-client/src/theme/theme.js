/* Centralized theme configuration for Ant Design ConfigProvider */
export const palette = {
  /* Brand */
  primary: '#4f9cf9',
  primaryHover: '#6db0ff',
  primaryActive: '#3a7fd4',

  /* Accent */
  green: '#3dbf50',
  greenDim: 'rgba(61,191,80,0.12)',
  red: '#f5222d',
  redDim: 'rgba(245,34,45,0.12)',
  blue: '#1890ff',
  orange: '#fa8c16',

  /* Background scale */
  bgBase: '#0f0f12',
  bgElevated: '#141419',
  bgCard: '#1a1a22',
  bgInput: '#12121a',
  bgActive: '#1e1e2a',
  bgHover: '#21212e',

  /* Text scale */
  textPrimary: '#e8e8f0',
  textSecondary: '#9898b0',
  textMuted: '#5a5a78',

  /* Border */
  border: '#2a2a3a',
  borderHover: '#4f9cf9',

  /* Misc */
  radius: 6,
  radiusLg: 10,
  radiusXl: 16,
};

/* Ant Design token overrides */
export const antdThemeTokens = {
  colorPrimary: palette.primary,
  colorSuccess: palette.green,
  colorWarning: palette.orange,
  colorError: palette.red,
  colorBgBase: palette.bgBase,
  colorBgContainer: palette.bgCard,
  colorBgElevated: palette.bgElevated,
  colorText: palette.textPrimary,
  colorTextSecondary: palette.textSecondary,
  colorBorder: palette.border,
  borderRadius: palette.radius,
  borderRadiusLG: palette.radiusLg,
  fontFamily: "'Inter', 'Segoe UI', system-ui, -apple-system, sans-serif",
  fontSize: 14,
  lineHeight: 1.6,
};
