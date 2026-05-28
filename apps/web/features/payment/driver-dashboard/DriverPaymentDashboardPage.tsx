'use client';

/**
 * Driver Payment Dashboard — shows recent payment notifications with
 * real-time WebSocket updates and TTS audio confirmations.
 *
 * Requirements: 3.3, 3.4, 3.8, 10.4
 */

import { useEffect, useRef, useCallback, useState } from 'react';
import { usePaymentListener } from './usePaymentListener';
import { useAudioConfirmation } from './useAudioConfirmation';
import { PaymentNotification } from './PaymentNotification';
import { AudioFallbackBanner } from './AudioFallbackBanner';
import type {
  PaymentConfirmedEvent,
  PaymentNotificationItem,
} from './types';

/**
 * Format centavos to Philippine Peso display string.
 */
function formatAmount(amountCentavos: number): string {
  const pesos = amountCentavos / 100;
  return `₱${pesos.toFixed(2)}`;
}

/**
 * Format an ISO timestamp to a human-readable local time string.
 */
function formatTimestamp(isoString: string): string {
  try {
    const date = new Date(isoString);
    return date.toLocaleTimeString(undefined, {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  } catch {
    return isoString;
  }
}

export function DriverPaymentDashboardPage() {
  const { payments, isConnected, error } = usePaymentListener();
  const { playConfirmation } = useAudioConfirmation();

  const [notifications, setNotifications] = useState<PaymentNotificationItem[]>(
    [],
  );
  const [fallbackBanner, setFallbackBanner] = useState<{
    payerName: string;
    formattedAmount: string;
  } | null>(null);

  // Track which events we've already processed audio for
  const processedEventsRef = useRef<Set<string>>(new Set());

  /**
   * Process a new payment event: attempt TTS, show fallback if needed.
   */
  const processPayment = useCallback(
    async (event: PaymentConfirmedEvent) => {
      if (processedEventsRef.current.has(event.eventId)) return;
      processedEventsRef.current.add(event.eventId);

      const audioPlayed = await playConfirmation(event);

      const notificationItem: PaymentNotificationItem = {
        eventId: event.eventId,
        payerName: event.payerName,
        formattedAmount: formatAmount(event.amountCentavos),
        amountCentavos: event.amountCentavos,
        formattedTime: formatTimestamp(event.occurredAt),
        occurredAt: event.occurredAt,
        audioPlayed,
      };

      setNotifications((prev) => [notificationItem, ...prev].slice(0, 50));

      // Req 3.8: Show high-contrast banner when audio fails
      if (!audioPlayed) {
        setFallbackBanner({
          payerName: event.payerName,
          formattedAmount: formatAmount(event.amountCentavos),
        });
      }
    },
    [playConfirmation],
  );

  // Process new payments as they arrive
  useEffect(() => {
    if (payments.length === 0) return;

    const latestPayment = payments[0];
    if (latestPayment && !processedEventsRef.current.has(latestPayment.eventId)) {
      processPayment(latestPayment);
    }
  }, [payments, processPayment]);

  const handleDismissBanner = useCallback(() => {
    setFallbackBanner(null);
  }, []);

  return (
    <div className="flex min-h-screen flex-col bg-gray-50 dark:bg-slate-900 p-4">
      {/* Audio fallback banner */}
      {fallbackBanner && (
        <AudioFallbackBanner
          payerName={fallbackBanner.payerName}
          formattedAmount={fallbackBanner.formattedAmount}
          onDismiss={handleDismissBanner}
        />
      )}

      {/* Header */}
      <header className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
          Payment Dashboard
        </h1>
        <div className="mt-2 flex items-center gap-2">
          <span
            className={`inline-block h-3 w-3 rounded-full ${
              isConnected ? 'bg-green-500' : 'bg-red-500'
            }`}
            aria-hidden="true"
          />
          <span className="text-sm text-gray-600 dark:text-white/60">
            {isConnected ? 'Connected' : 'Disconnected'}
          </span>
        </div>
        {error && (
          <p className="mt-1 text-sm text-red-600 dark:text-red-400" role="alert">
            {error}
          </p>
        )}
      </header>

      {/* Notification list */}
      <main className="flex flex-1 flex-col gap-3" aria-label="Payment notifications">
        {notifications.length === 0 ? (
          <div className="flex flex-1 items-center justify-center">
            <p className="text-center text-gray-500 dark:text-white/40">
              No payments received yet. Waiting for confirmations…
            </p>
          </div>
        ) : (
          notifications.map((notification) => (
            <PaymentNotification
              key={notification.eventId}
              notification={notification}
            />
          ))
        )}
      </main>
    </div>
  );
}
