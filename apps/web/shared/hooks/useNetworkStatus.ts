"use client";

import { useSyncExternalStore } from "react";

/**
 * Returns the current network connectivity status.
 * SSR-safe: defaults to `true` on the server (assumes online).
 *
 * Uses `navigator.onLine` and listens to `online`/`offline` events
 * for real-time updates.
 *
 * Satisfies Requirements 6.7, 6.8.
 */
export function useNetworkStatus(): { isOnline: boolean } {
  const isOnline = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
  return { isOnline };
}

function subscribe(callback: () => void): () => void {
  window.addEventListener("online", callback);
  window.addEventListener("offline", callback);
  return () => {
    window.removeEventListener("online", callback);
    window.removeEventListener("offline", callback);
  };
}

function getSnapshot(): boolean {
  return navigator.onLine;
}

function getServerSnapshot(): boolean {
  return true;
}
