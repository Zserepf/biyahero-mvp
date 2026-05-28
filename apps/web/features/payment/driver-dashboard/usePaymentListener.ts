'use client';

/**
 * WebSocket hook that listens for `payment.confirmed` events.
 *
 * Connects to the WebSocket API using the stored JWT, parses incoming
 * payment.confirmed envelopes, and exposes them as a reactive list.
 *
 * Requirements: 3.3
 */

import { useState, useEffect, useRef, useCallback } from 'react';
import { env } from '@/infrastructure/config/env';
import { ACCESS_TOKEN_KEY } from '@/infrastructure/api/client';
import type { PaymentConfirmedEvent } from './types';

interface UsePaymentListenerReturn {
  /** List of received payment events (newest first) */
  payments: PaymentConfirmedEvent[];
  /** Whether the WebSocket is currently connected */
  isConnected: boolean;
  /** Last connection error message, if any */
  error: string | null;
}

/** Maximum number of payment notifications to keep in memory */
const MAX_NOTIFICATIONS = 50;

/**
 * Reconnection delay in milliseconds (exponential backoff capped at 30s).
 */
function getReconnectDelay(attempt: number): number {
  return Math.min(1000 * Math.pow(2, attempt), 30_000);
}

export function usePaymentListener(): UsePaymentListenerReturn {
  const [payments, setPayments] = useState<PaymentConfirmedEvent[]>([]);
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const wsRef = useRef<WebSocket | null>(null);
  const reconnectAttemptRef = useRef(0);
  const reconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const mountedRef = useRef(true);

  const connect = useCallback(() => {
    const token =
      typeof window !== 'undefined'
        ? localStorage.getItem(ACCESS_TOKEN_KEY)
        : null;

    if (!token) {
      setError('Not authenticated');
      return;
    }

    // Close existing connection if any
    if (wsRef.current) {
      wsRef.current.close();
      wsRef.current = null;
    }

    try {
      const ws = new WebSocket(`${env.WS_URL}?token=${token}`);
      wsRef.current = ws;

      ws.onopen = () => {
        if (!mountedRef.current) return;
        setIsConnected(true);
        setError(null);
        reconnectAttemptRef.current = 0;
      };

      ws.onmessage = (event: MessageEvent) => {
        if (!mountedRef.current) return;

        try {
          const envelope = JSON.parse(event.data as string) as {
            action: string;
            data: PaymentConfirmedEvent;
          };

          if (envelope.action === 'payment.confirmed' && envelope.data) {
            setPayments((prev) => {
              // Deduplicate by eventId
              if (prev.some((p) => p.eventId === envelope.data.eventId)) {
                return prev;
              }
              const updated = [envelope.data, ...prev];
              return updated.slice(0, MAX_NOTIFICATIONS);
            });
          }
        } catch {
          // Ignore malformed messages
        }
      };

      ws.onclose = () => {
        if (!mountedRef.current) return;
        setIsConnected(false);

        // Attempt reconnection with exponential backoff
        const delay = getReconnectDelay(reconnectAttemptRef.current);
        reconnectAttemptRef.current += 1;

        reconnectTimerRef.current = setTimeout(() => {
          if (mountedRef.current) {
            connect();
          }
        }, delay);
      };

      ws.onerror = () => {
        if (!mountedRef.current) return;
        setError('WebSocket connection error');
      };
    } catch (err) {
      setError(
        err instanceof Error ? err.message : 'Failed to connect to WebSocket',
      );
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    connect();

    return () => {
      mountedRef.current = false;
      if (reconnectTimerRef.current) {
        clearTimeout(reconnectTimerRef.current);
      }
      if (wsRef.current) {
        wsRef.current.close();
        wsRef.current = null;
      }
    };
  }, [connect]);

  return { payments, isConnected, error };
}
