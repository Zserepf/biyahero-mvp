/**
 * Vitest global test setup.
 *
 * - Configures @testing-library/jest-dom matchers
 * - Sets up MSW server for API mocking
 * - Provides environment variable stubs
 */

import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll, vi } from 'vitest';
import { cleanup } from '@testing-library/react';
import { server } from './mocks/server';

// ─── Environment Variables ───────────────────────────────────────────────────

// Stub process.env for the env.ts module
process.env.NEXT_PUBLIC_API_URL = 'http://localhost:3001';
process.env.NEXT_PUBLIC_WS_URL = 'ws://localhost:3001/ws';

// ─── MSW Server Lifecycle ────────────────────────────────────────────────────

beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }));
afterEach(() => {
  server.resetHandlers();
  cleanup();
});
afterAll(() => server.close());

// ─── Mock next-intl ──────────────────────────────────────────────────────────

vi.mock('next-intl', () => ({
  useTranslations: () => (key: string) => key,
}));
