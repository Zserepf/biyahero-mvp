"use client";

import { useEffect, useState } from "react";
import { registerServiceWorker, onSwUpdate, applySwUpdate } from "../../lib/sw-registration";

/**
 * Non-blocking banner that appears when a new Service Worker version is
 * waiting to activate. Offers the user a reload button to apply the update.
 * Dismissible via a "Later" button.
 *
 * Satisfies Requirement 6.7: "IF a Service_Worker update is available, THEN
 * THE Frontend SHALL prompt the user with a non-blocking banner offering to
 * reload the app to apply the update."
 */
export default function SwUpdateBanner() {
  const [waitingRegistration, setWaitingRegistration] = useState<ServiceWorkerRegistration | null>(
    null,
  );

  useEffect(() => {
    onSwUpdate((registration) => {
      setWaitingRegistration(registration);
    });

    registerServiceWorker();
  }, []);

  if (!waitingRegistration) {
    return null;
  }

  const handleReload = () => {
    applySwUpdate(waitingRegistration);
  };

  const handleDismiss = () => {
    setWaitingRegistration(null);
  };

  return (
    <div
      role="alert"
      aria-live="polite"
      className="fixed bottom-4 left-4 right-4 z-50 mx-auto max-w-md rounded-lg border border-blue-200 dark:border-blue-500/30 bg-blue-50 dark:bg-blue-500/10 p-4 shadow-lg dark:shadow-none sm:left-auto sm:right-4"
    >
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm font-medium text-blue-900 dark:text-blue-300">
          A new version is available. Refresh to update.
        </p>
        <div className="flex shrink-0 gap-2">
          <button
            onClick={handleReload}
            className="rounded-md bg-blue-600 px-3 py-1.5 text-xs font-semibold text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 dark:focus:ring-offset-slate-900"
            aria-label="Refresh to update BiyaHero"
          >
            Refresh
          </button>
          <button
            onClick={handleDismiss}
            className="rounded-md border border-blue-300 dark:border-blue-500/40 bg-white dark:bg-blue-500/10 px-3 py-1.5 text-xs font-semibold text-blue-700 dark:text-blue-300 hover:bg-blue-100 dark:hover:bg-blue-500/20 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 dark:focus:ring-offset-slate-900"
            aria-label="Dismiss update notification"
          >
            Later
          </button>
        </div>
      </div>
    </div>
  );
}
