/**
 * Offline resilience module.
 *
 * Provides an IndexedDB-backed write queue and an Axios interceptor
 * that transparently queues failed write requests when offline and
 * replays them in FIFO order when connectivity is restored.
 *
 * Usage (in app initialization):
 *
 *   import { apiClient } from '@/infrastructure/api/client';
 *   import { installOfflineInterceptor, initOfflineQueue } from '@/infrastructure/offline';
 *
 *   // Install the interceptor on the shared Axios instance
 *   installOfflineInterceptor(apiClient);
 *
 *   // Start listening for online/offline events
 *   initOfflineQueue();
 */

export {
  installOfflineInterceptor,
  triggerReplay,
} from './axios-offline-interceptor';

export {
  enqueue,
  dequeue,
  getAll,
  clear,
  removeById,
  replay,
  getQueueStatus,
  getDeadLetters,
  clearDeadLetters,
  initOfflineQueue,
  destroyOfflineQueue,
  setReplayExecutor,
  type QueuedWriteRequest,
  type QueueStatus,
  type ReplayExecutor,
  MAX_RETRIES,
} from './write-queue';
