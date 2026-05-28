"use client";

import { useEffect, useRef, useState } from "react";
import { useNetworkStatus } from "../hooks/useNetworkStatus";

/**
 * Displays a non-intrusive banner/toast when the user goes offline,
 * and a brief "Back online" message when connectivity is restored.
 *
 * Satisfies Requirement 6.8: "THE Frontend SHALL display a clearly visible
 * offline indicator whenever the device reports navigator.onLine === false."
 */
export default function NetworkStatus() {
  const { isOnline } = useNetworkStatus();
  const [showBackOnline, setShowBackOnline] = useState(false);
  const wasOffline = useRef(false);

  useEffect(() => {
    if (!isOnline) {
      wasOffline.current = true;
      setShowBackOnline(false);
    } else if (wasOffline.current) {
      // Just came back online
      wasOffline.current = false;
      setShowBackOnline(true);

      const timer = setTimeout(() => {
        setShowBackOnline(false);
      }, 3000);

      return () => clearTimeout(timer);
    }
  }, [isOnline]);

  if (!isOnline) {
    return (
      <div
        role="status"
        aria-live="polite"
        aria-atomic="true"
        className="fixed top-0 left-0 right-0 z-50 bg-amber-600 px-4 py-2 text-center shadow-md"
      >
        <p className="text-sm font-medium text-white">
          You are offline. Changes will be saved locally.
        </p>
      </div>
    );
  }

  if (showBackOnline) {
    return (
      <div
        role="status"
        aria-live="polite"
        aria-atomic="true"
        className="fixed top-0 left-0 right-0 z-50 bg-green-600 px-4 py-2 text-center shadow-md transition-opacity duration-300"
      >
        <p className="text-sm font-medium text-white">Back online</p>
      </div>
    );
  }

  return null;
}
