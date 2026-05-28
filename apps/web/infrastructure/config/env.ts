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

function requireEnv(key: string): string {
  const value = process.env[key];
  if (!value) {
    throw new Error(
      `Missing required environment variable: ${key}. ` +
        `Add it to your .env.local file.`,
    );
  }
  return value;
}

export const env: Env = {
  API_URL: requireEnv('NEXT_PUBLIC_API_URL'),
  WS_URL: requireEnv('NEXT_PUBLIC_WS_URL'),
};
