import axios, {
  AxiosError,
  type AxiosInstance,
  type InternalAxiosRequestConfig,
} from 'axios';

import { env } from '@/infrastructure/config/env';
import { ApiError, type ApiErrorEnvelope } from '@/shared/types/api';

/**
 * Token storage key used by the auth layer.
 * The auth feature writes the JWT here after login; the interceptor reads it.
 */
const ACCESS_TOKEN_KEY = 'biyahero_access_token';

/**
 * Read the current JWT access token from localStorage.
 * Returns null when running server-side or when no token is stored.
 */
function getAccessToken(): string | null {
  if (typeof window === 'undefined') return null;
  return localStorage.getItem(ACCESS_TOKEN_KEY);
}

// ─── Axios Instance ──────────────────────────────────────────────────────────

const apiClient: AxiosInstance = axios.create({
  baseURL: env.API_URL,
  headers: {
    'Content-Type': 'application/json',
    Accept: 'application/json',
  },
  timeout: 30_000,
});

// ─── Request Interceptor: JWT Bearer ─────────────────────────────────────────

apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = getAccessToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error: unknown) => Promise.reject(error),
);

// ─── Response Interceptor: Centralized Error Transform ───────────────────────

apiClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (error instanceof AxiosError && error.response) {
      const { status, data } = error.response;

      // Attempt to parse the backend error envelope
      const envelope = data as ApiErrorEnvelope | undefined;

      if (envelope?.error) {
        return Promise.reject(
          new ApiError(
            status,
            envelope.error.code,
            envelope.error.message,
            envelope.error.details,
          ),
        );
      }

      // Non-envelope error response (unexpected format)
      return Promise.reject(
        new ApiError(
          status,
          'server.unknown',
          error.message || 'An unexpected error occurred',
        ),
      );
    }

    // Network error or request cancelled (no response received)
    if (error instanceof AxiosError && !error.response) {
      return Promise.reject(
        new ApiError(
          0,
          'network.error',
          error.message || 'Network error — check your connection',
        ),
      );
    }

    // Fallback for non-Axios errors
    return Promise.reject(
      new ApiError(
        0,
        'client.unknown',
        error instanceof Error ? error.message : 'Unknown error',
      ),
    );
  },
);

export { apiClient, ACCESS_TOKEN_KEY };
