/**
 * IndexedDB-based offline write queue.
 *
 * Stores pending write requests when the app is offline (navigator.onLine === false
 * or network error). On reconnect (online event), replays queued writes in submission
 * order (FIFO by timestamp). Successfully replayed items are removed from the queue.
 * Failed replays are retried with exponential backoff up to MAX_RETRIES times,
 * after which they are moved to a dead-letter store.
 *
 * Requirement: 6.4
 */

import { openDB, type IDBPDatabase } from 'idb';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface QueuedWriteRequest {
  /** Auto-generated unique ID */
  id: string;
  /** Submission timestamp (epoch ms) — used for FIFO ordering */
  timestamp: number;
  /** API endpoint path */
  url: string;
  /** HTTP method (POST, PUT, PATCH, DELETE) */
  method: 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  /** Request body (serialized JSON) */
  body: unknown;
  /** Request headers (excluding Authorization — re-attached at replay time) */
  headers: Record<string, string>;
  /** Number of replay attempts so far */
  retryCount: number;
}

export interface QueueStatus {
  /** Number of pending items in the queue */
  pendingCount: number;
  /** Number of items in the dead-letter store */
  deadLetterCount: number;
  /** Whether a replay is currently in progress */
  isReplaying: boolean;
}

// ─── Constants ───────────────────────────────────────────────────────────────

const DB_NAME = 'biyahero-offline';
const DB_VERSION = 2;
const STORE_NAME = 'write-queue';
const DEAD_LETTER_STORE = 'dead-letter';

/** Maximum number of retry attempts before moving to dead-letter store */
const MAX_RETRIES = 5;

/** Base delay for exponential backoff (ms) */
const BASE_BACKOFF_MS = 1000;

// ─── Database Setup ──────────────────────────────────────────────────────────

let dbPromise: Promise<IDBPDatabase> | null = null;

function getDb(): Promise<IDBPDatabase> {
  if (!dbPromise) {
    dbPromise = openDB(DB_NAME, DB_VERSION, {
      upgrade(db) {
        if (!db.objectStoreNames.contains(STORE_NAME)) {
          const store = db.createObjectStore(STORE_NAME, { keyPath: 'id' });
          store.createIndex('by-timestamp', 'timestamp');
        }
        if (!db.objectStoreNames.contains(DEAD_LETTER_STORE)) {
          const dlStore = db.createObjectStore(DEAD_LETTER_STORE, {
            keyPath: 'id',
          });
          dlStore.createIndex('by-timestamp', 'timestamp');
        }
      },
    });
  }
  return dbPromise;
}

// ─── Queue State ─────────────────────────────────────────────────────────────

let isReplaying = false;
let replayTimeoutId: ReturnType<typeof setTimeout> | null = null;

// ─── Public API ──────────────────────────────────────────────────────────────

/**
 * Generate a unique ID for a queued request.
 */
function generateId(): string {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 11)}`;
}

/**
 * Enqueue a write request for later replay.
 */
export async function enqueue(
  request: Omit<QueuedWriteRequest, 'id' | 'timestamp' | 'retryCount'>,
): Promise<QueuedWriteRequest> {
  const db = await getDb();
  const item: QueuedWriteRequest = {
    id: generateId(),
    timestamp: Date.now(),
    retryCount: 0,
    ...request,
  };
  await db.put(STORE_NAME, item);
  return item;
}

/**
 * Remove and return the oldest request from the queue (FIFO).
 */
export async function dequeue(): Promise<QueuedWriteRequest | undefined> {
  const db = await getDb();
  const tx = db.transaction(STORE_NAME, 'readwrite');
  const store = tx.objectStore(STORE_NAME);
  const index = store.index('by-timestamp');
  const cursor = await index.openCursor();

  if (!cursor) {
    await tx.done;
    return undefined;
  }

  const item = cursor.value as QueuedWriteRequest;
  await cursor.delete();
  await tx.done;
  return item;
}

/**
 * Get all queued requests in FIFO order (oldest first).
 */
export async function getAll(): Promise<QueuedWriteRequest[]> {
  const db = await getDb();
  return db.getAllFromIndex(STORE_NAME, 'by-timestamp');
}

/**
 * Remove all queued requests.
 */
export async function clear(): Promise<void> {
  const db = await getDb();
  await db.clear(STORE_NAME);
}

/**
 * Remove a specific item from the queue by ID.
 */
export async function removeById(id: string): Promise<void> {
  const db = await getDb();
  await db.delete(STORE_NAME, id);
}

/**
 * Increment the retry count for a failed replay item.
 */
async function incrementRetryCount(id: string): Promise<number> {
  const db = await getDb();
  const tx = db.transaction(STORE_NAME, 'readwrite');
  const store = tx.objectStore(STORE_NAME);
  const item = await store.get(id);
  if (item) {
    item.retryCount += 1;
    await store.put(item);
    await tx.done;
    return item.retryCount;
  }
  await tx.done;
  return 0;
}

/**
 * Move an item to the dead-letter store (exceeded max retries).
 */
async function moveToDeadLetter(item: QueuedWriteRequest): Promise<void> {
  const db = await getDb();
  const tx = db.transaction([STORE_NAME, DEAD_LETTER_STORE], 'readwrite');
  await tx.objectStore(STORE_NAME).delete(item.id);
  await tx.objectStore(DEAD_LETTER_STORE).put(item);
  await tx.done;
}

/**
 * Get all items in the dead-letter store.
 */
export async function getDeadLetters(): Promise<QueuedWriteRequest[]> {
  const db = await getDb();
  return db.getAllFromIndex(DEAD_LETTER_STORE, 'by-timestamp');
}

/**
 * Clear the dead-letter store.
 */
export async function clearDeadLetters(): Promise<void> {
  const db = await getDb();
  await db.clear(DEAD_LETTER_STORE);
}

/**
 * Get the current queue status.
 */
export async function getQueueStatus(): Promise<QueueStatus> {
  const db = await getDb();
  const pendingCount = await db.count(STORE_NAME);
  const deadLetterCount = await db.count(DEAD_LETTER_STORE);
  return {
    pendingCount,
    deadLetterCount,
    isReplaying,
  };
}

// ─── Replay Engine ───────────────────────────────────────────────────────────

export type ReplayExecutor = (
  request: QueuedWriteRequest,
) => Promise<{ success: boolean }>;

let replayExecutor: ReplayExecutor | null = null;

/**
 * Register the function that actually sends queued requests to the server.
 * This is called once during app initialization with the Axios-based sender.
 */
export function setReplayExecutor(executor: ReplayExecutor): void {
  replayExecutor = executor;
}

/**
 * Replay all queued writes in strict submission order (FIFO).
 *
 * Processing is sequential — each request must complete before the next starts.
 * On success, the item is removed from the queue.
 * On failure, the retry count is incremented. If it exceeds MAX_RETRIES,
 * the item is moved to the dead-letter store. Otherwise, replay stops and
 * a retry is scheduled with exponential backoff.
 */
export async function replay(): Promise<void> {
  if (isReplaying) return;
  if (!replayExecutor) return;
  if (typeof navigator !== 'undefined' && !navigator.onLine) return;

  isReplaying = true;

  try {
    const pending = await getAll();

    for (const item of pending) {
      // Check connectivity before each request
      if (typeof navigator !== 'undefined' && !navigator.onLine) {
        break;
      }

      const result = await replayExecutor(item);

      if (result.success) {
        await removeById(item.id);
      } else {
        const newRetryCount = await incrementRetryCount(item.id);

        if (newRetryCount >= MAX_RETRIES) {
          // Move to dead-letter store after max retries exceeded
          const updatedItem = { ...item, retryCount: newRetryCount };
          await moveToDeadLetter(updatedItem);
        } else {
          // Stop processing and schedule retry with backoff
          scheduleRetry(newRetryCount);
          break;
        }
      }
    }
  } finally {
    isReplaying = false;
  }
}

/**
 * Schedule a retry with exponential backoff.
 */
function scheduleRetry(retryCount: number): void {
  if (replayTimeoutId !== null) {
    clearTimeout(replayTimeoutId);
  }

  const delay = Math.min(
    BASE_BACKOFF_MS * Math.pow(2, retryCount - 1),
    30_000, // Cap at 30 seconds
  );

  replayTimeoutId = setTimeout(() => {
    replayTimeoutId = null;
    replay();
  }, delay);
}

// ─── Online/Offline Event Listeners ──────────────────────────────────────────

let listenersRegistered = false;

/**
 * Initialize the offline write queue listeners.
 * Call once during app startup (e.g., in a top-level provider or layout).
 */
export function initOfflineQueue(): void {
  if (typeof window === 'undefined') return;
  if (listenersRegistered) return;

  window.addEventListener('online', handleOnline);
  listenersRegistered = true;
}

/**
 * Tear down the offline write queue listeners.
 */
export function destroyOfflineQueue(): void {
  if (typeof window === 'undefined') return;
  if (!listenersRegistered) return;

  window.removeEventListener('online', handleOnline);
  listenersRegistered = false;

  if (replayTimeoutId !== null) {
    clearTimeout(replayTimeoutId);
    replayTimeoutId = null;
  }
}

function handleOnline(): void {
  // Small delay to let the connection stabilize
  setTimeout(() => {
    replay();
  }, 500);
}

export { MAX_RETRIES, BASE_BACKOFF_MS, DB_NAME, STORE_NAME, DEAD_LETTER_STORE };
