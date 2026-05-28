/**
 * Axios interceptor that queues write requests when the app is offline.
 *
 * Integrates with the IndexedDB write queue to transparently capture
 * POST, PUT, PATCH, DELETE requests that fail due to network errors
 * while the device is offline. When connectivity is restored, queued
 * requests are replayed in strict FIFO order.
 *
 * Requirement: 6.4
 */

import type { AxiosError, AxiosInstance, AxiosResponse } from 'axios';

import {
  enqueue,
  setReplayExecutor,
  replay,
  type QueuedWriteRequest,
} from './write-queue';

/** HTTP methods that represent write operations eligible for offline queueing */
const WRITE_METHODS = new Set(['post', 'put', 'patch', 'delete']);

/** Custom Axios config flag to prevent re-queueing replayed requests */
const REPLAY_FLAG = '__biyahero_replay';

/**
 * Determine if a request is a write operation that should be queued when offline.
 */
function isWriteRequest(method: string | undefined): boolean {
  return WRITE_METHODS.has((method ?? '').toLowerCase());
}

/**
 * Determine if an error is a network error (no response received).
 */
function isNetworkError(error: AxiosError): boolean {
  // No response means the request never reached the server
  if (error.response) return false;
  // Explicit network error code from Axios
  if (error.code === 'ERR_NETWORK') return true;
  // Browser reports offline
  if (typeof navigator !== 'undefined' && !navigator.onLine) return true;
  return false;
}

/**
 * Install the offline write interceptor on an Axios instance.
 *
 * When a write request fails due to a network error while offline,
 * the request is queued in IndexedDB for later replay.
 *
 * Returns a cleanup function to eject the interceptor.
 */
export function installOfflineInterceptor(client: AxiosInstance): () => void {
  // Register the replay executor that uses the same Axios instance
  setReplayExecutor(createReplayExecutor(client));

  const interceptorId = client.interceptors.response.use(
    // Success — pass through
    (response: AxiosResponse) => response,
    // Error — check if we should queue
    async (error: AxiosError) => {
      const config = error.config;

      // Don't re-queue replayed requests
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      if (config && (config as any)[REPLAY_FLAG]) {
        return Promise.reject(error);
      }

      // Only intercept write requests that failed due to network issues
      if (config && isWriteRequest(config.method) && isNetworkError(error)) {
        // Extract headers we want to preserve (skip Authorization — re-attached at replay)
        const headers: Record<string, string> = {};
        if (config.headers?.['Content-Type']) {
          headers['Content-Type'] = config.headers['Content-Type'] as string;
        }
        if (config.headers?.['Accept']) {
          headers['Accept'] = config.headers['Accept'] as string;
        }

        let body: unknown = null;
        if (config.data) {
          try {
            body =
              typeof config.data === 'string'
                ? JSON.parse(config.data)
                : config.data;
          } catch {
            body = config.data;
          }
        }

        const queued = await enqueue({
          url: config.url ?? '',
          method: (config.method?.toUpperCase() ?? 'POST') as
            | 'POST'
            | 'PUT'
            | 'PATCH'
            | 'DELETE',
          body,
          headers,
        });

        // Return a synthetic response indicating the request was queued
        return Promise.resolve({
          data: { queued: true, queueId: queued.id },
          status: 202,
          statusText: 'Queued for offline replay',
          headers: {},
          config,
        } as AxiosResponse);
      }

      // Not a queueable error — reject normally
      return Promise.reject(error);
    },
  );

  return () => {
    client.interceptors.response.eject(interceptorId);
  };
}

/**
 * Create a replay executor that sends queued requests through the Axios client.
 * The replay flag prevents the offline interceptor from re-queueing failed replays.
 */
function createReplayExecutor(
  client: AxiosInstance,
): (request: QueuedWriteRequest) => Promise<{ success: boolean }> {
  return async (request: QueuedWriteRequest) => {
    try {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      await client.request({
        url: request.url,
        method: request.method.toLowerCase(),
        data: request.body,
        headers: {
          ...request.headers,
          // Authorization header is re-attached by the existing request interceptor
        },
        // Mark as replay to prevent re-queueing on failure
        [REPLAY_FLAG]: true,
      } as any);
      return { success: true };
    } catch {
      return { success: false };
    }
  };
}

/**
 * Trigger a manual replay of the offline queue.
 * Useful when the app detects connectivity has been restored.
 */
export function triggerReplay(): void {
  replay();
}
