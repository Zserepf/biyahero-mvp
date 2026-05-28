/**
 * All API path constants for the BiyaHero REST surface.
 *
 * When a backend route changes, update this one file only.
 * Never use inline API URL strings — always use API_ENDPOINTS.
 */

export const API_ENDPOINTS = {
  // ─── Auth ──────────────────────────────────────────────────────────────
  AUTH: {
    REGISTER: '/v1/auth/registrations',
    VERIFY_EMAIL: '/v1/auth/email-verifications/:verify',
    LOGIN: '/v1/auth/sessions',
    REFRESH: '/v1/auth/sessions/:refresh',
    LOGOUT: (id: string) => `/v1/auth/sessions/${id}`,
    ME: '/v1/auth/me',
    LANGUAGE_PREFERENCE: '/v1/auth/me/language-preference',
  },

  // ─── Routes ────────────────────────────────────────────────────────────
  ROUTES: {
    LIST: '/v1/routes',
    CREATE: '/v1/routes',
    GET: (id: string) => `/v1/routes/${id}`,
    CREATE_REVISION: (routeId: string) => `/v1/routes/${routeId}/revisions`,
    APPROVE_REVISION: (routeId: string, revisionId: string) =>
      `/v1/routes/${routeId}/revisions/${revisionId}/:approve`,
    VOTE: (routeId: string) => `/v1/routes/${routeId}/votes`,
  },

  // ─── Fare ──────────────────────────────────────────────────────────────
  FARE: {
    CALCULATE: '/v1/fare/:calculate',
  },

  // ─── Heatmap ───────────────────────────────────────────────────────────
  HEATMAP: {
    TILES: '/v1/heatmap/tiles',
  },

  // ─── Payments ──────────────────────────────────────────────────────────
  PAYMENTS: {
    WEBHOOK: '/v1/payments/webhook',
    AUDIO_FAILURES: '/v1/payments/audio-failures',
  },

  // ─── Admin ─────────────────────────────────────────────────────────────
  ADMIN: {
    USERS: '/v1/admin/users',
    SUSPEND_USER: (id: string) => `/v1/admin/users/${id}/:suspend`,
    PROMOTE_USER: (id: string) => `/v1/admin/users/${id}/:promote`,
  },

  // ─── i18n ──────────────────────────────────────────────────────────────
  I18N: {
    MISSING_KEYS: '/v1/i18n/missing-keys',
  },

  // ─── Health ────────────────────────────────────────────────────────────
  HEALTH: {
    CHECK: '/v1/health',
  },
} as const;
