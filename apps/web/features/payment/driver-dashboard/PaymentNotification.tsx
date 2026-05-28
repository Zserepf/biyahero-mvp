'use client';

/**
 * Single payment notification card — displays payer name, amount (₱XX.XX),
 * and timestamp for a confirmed payment.
 *
 * Requirement: 3.3
 */

import type { PaymentNotificationItem } from './types';

interface PaymentNotificationProps {
  notification: PaymentNotificationItem;
}

export function PaymentNotification({
  notification,
}: PaymentNotificationProps) {
  return (
    <article
      className="flex items-center justify-between rounded-lg border border-green-200 bg-green-50 p-4 shadow-sm"
      aria-label={`Payment from ${notification.payerName}, ${notification.formattedAmount}`}
    >
      <div className="flex flex-col gap-1">
        <span className="text-base font-semibold text-gray-900">
          {notification.payerName}
        </span>
        <time
          className="text-sm text-gray-600"
          dateTime={notification.occurredAt}
        >
          {notification.formattedTime}
        </time>
      </div>

      <div className="flex flex-col items-end gap-1">
        <span
          className="text-lg font-bold text-green-700"
          aria-label={`Amount: ${notification.formattedAmount}`}
        >
          {notification.formattedAmount}
        </span>
        {!notification.audioPlayed && (
          <span
            className="text-xs text-amber-600"
            aria-label="Audio confirmation was not played"
          >
            🔇 No audio
          </span>
        )}
      </div>
    </article>
  );
}
