/// <reference lib="webworker" />

declare const self: ServiceWorkerGlobalScope;

/**
 * Custom Service Worker code injected by next-pwa.
 *
 * Listens for the SKIP_WAITING message from the client so the update banner
 * can trigger activation of the new SW version (Req 6.7).
 */
self.addEventListener("message", (event) => {
  if (event.data && event.data.type === "SKIP_WAITING") {
    self.skipWaiting();
  }
});

export {};
