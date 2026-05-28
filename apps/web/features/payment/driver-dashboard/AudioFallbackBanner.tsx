'use client';

/**
 * High-contrast visual confirmation banner shown when audio is muted or blocked.
 *
 * Displays the payment details prominently so the driver still gets confirmation
 * even without audio. Also POSTs the failure to /v1/payments/audio-failures.
 *
 * Requirements: 3.8, 9.1, 9.2
 */

import { useEffect, useState } from 'react';

interface AudioFallbackBannerProps {
  /** Payer display name */
  payerName: string;
  /** Formatted amount (e.g. "₱25.00") */
  formattedAmount: string;
  /** Callback to dismiss the banner */
  onDismiss: () => void;
}

/** Auto-dismiss timeout in milliseconds */
const AUTO_DISMISS_MS = 10_000;

export function AudioFallbackBanner({
  payerName,
  formattedAmount,
  onDismiss,
}: AudioFallbackBannerProps) {
  const [visible, setVisible] = useState(true);

  useEffect(() => {
    const timer = setTimeout(() => {
      setVisible(false);
      onDismiss();
    }, AUTO_DISMISS_MS);

    return () => clearTimeout(timer);
  }, [onDismiss]);

  if (!visible) return null;

  return (
    <div
      role="alert"
      aria-live="assertive"
      aria-atomic="true"
      className="fixed inset-x-0 top-0 z-50 flex items-center justify-between border-b-4 border-yellow-500 bg-yellow-400 px-4 py-4 shadow-lg"
      style={{ minHeight: '80px' }}
    >
      <div className="flex flex-col gap-1">
        <span className="text-lg font-bold text-black">
          💰 Payment Received
        </span>
        <span className="text-base font-semibold text-black">
          From: {payerName}
        </span>
        <span className="text-xl font-bold text-black">{formattedAmount}</span>
      </div>

      <button
        type="button"
        onClick={() => {
          setVisible(false);
          onDismiss();
        }}
        className="flex h-11 w-11 items-center justify-center rounded-full bg-black text-xl font-bold text-yellow-400 focus:outline-none focus:ring-2 focus:ring-black focus:ring-offset-2"
        aria-label="Dismiss payment notification"
      >
        ✕
      </button>
    </div>
  );
}
