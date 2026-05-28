/**
 * Typed environment variables for the BiyaHero PWA.
 *
 * All environment access is centralized here — no raw `process.env`
 * usage outside this file.
 */

interface Env {
  /** Base URL for the REST API (e.g. https://api.biyahero.app/v1) */
  API_URL: string;
  /** Base URL for the WebSocket API (e.g. wss://ws.biyahero.app/prod) */
  WS_URL: string;
}

const DEFAULTS: Record<string, string> = {
  NEXT_PUBLIC_API_URL: 'http://localhost:5000',
  NEXT_PUBLIC_WS_URL: 'ws://localhost:5000/ws',
};

function getEnv(key: string): string {
  return process.env[key] || DEFAULTS[key] || '';
}

export const env: Env = {
  API_URL: getEnv('NEXT_PUBLIC_API_URL'),
  WS_URL: getEnv('NEXT_PUBLIC_WS_URL'),
};
