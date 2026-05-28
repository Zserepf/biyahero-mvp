'use client';

/**
 * useCommuterHeatmap — WebSocket hook for commuter demand-ping flow.
 *
 * Connects to the WebSocket API on mount using the JWT token from localStorage.
 * Provides methods to submit a demand-ping and cancel an active ping.
 * Automatically disconnects on unmount.
 *
 * Requirements: 4.1, 4.5
 */

import { useEffect, useRef, useCallback, useState } from 'react';
import { env } from '@/infrastructure/config/env';
import type {
  ConnectionStatus,
  DemandPingRequest,
  DemandPingResponse,
  CancelDemandResponse,
  WsEnvelope,
} from './types';

const ACCESS_TOKEN_KEY = 'biyahero_access_token';

function generateRequestId(): string {
  return crypto.randomUUID();
}

interface UseCommuterHeatmapReturn {
  /** Current WebSocket connection status */
  status: ConnectionStatus;
  /** The active demand ping (null if none) */
  activePing: DemandPingResponse | null;
  /** Submit a demand-ping with lat/lng/vehicleType */
  submitDemandPing: (data: DemandPingRequest) => void;
  /** Cancel the active demand ping */
  cancelDemand: () => void;
  /** Last error message (null if no error) */
  error: string | null;
}

export function useCommuterHeatmap(): UseCommuterHeatmapReturn {
  const [status, setStatus] = useState<ConnectionStatus>('disconnected');
  const [activePing, setActivePing] = useState<DemandPingResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const wsRef = useRef<WebSocket | null>(null);
  const reconnectTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // ─── Connect on mount ────────────────────────────────────────────────

  const connect = useCallback(() => {
    const token = typeof window !== 'undefined'
      ? localStorage.getItem(ACCESS_TOKEN_KEY)
      : null;

    if (!token) {
      setError('Authentication required. Please log in.');
      setStatus('error');
      return;
    }

    // Close existing connection if any
    if (wsRef.current) {
      wsRef.current.close();
      wsRef.current = null;
    }

    setStatus('connecting');
    setError(null);

    const wsUrl = `${env.WS_URL}?token=${encodeURIComponent(token)}`;
    const ws = new WebSocket(wsUrl);

    ws.onopen = () => {
      setStatus('connected');
      setError(null);
    };

    ws.onmessage = (event) => {
      try {
        const envelope: WsEnvelope = JSON.parse(event.data);

        switch (envelope.action) {
          case 'demand-ping': {
            const pingData = envelope.data as DemandPingResponse;
            setActivePing(pingData);
            break;
          }
          case 'cancel-demand': {
            const cancelData = envelope.data as CancelDemandResponse;
            if (cancelData.cancelled) {
              setActivePing(null);
            }
            break;
          }
          case 'error': {
            const errorData = envelope.data as { message?: string };
            setError(errorData.message || 'An error occurred');
            break;
          }
          default:
            // Ignore unrecognized actions
            break;
        }
      } catch {
        // Ignore malformed messages
      }
    };

    ws.onerror = () => {
      setError('WebSocket connection error');
      setStatus('error');
    };

    ws.onclose = (event) => {
      wsRef.current = null;
      setStatus('disconnected');

      // 4001 = auth failure, don't reconnect
      if (event.code === 4001) {
        setError('Authentication failed. Please log in again.');
        return;
      }

      // Attempt reconnect after 3 seconds for unexpected disconnects
      if (event.code !== 1000) {
        reconnectTimeoutRef.current = setTimeout(() => {
          connect();
        }, 3000);
      }
    };

    wsRef.current = ws;
  }, []);

  // ─── Lifecycle ───────────────────────────────────────────────────────

  useEffect(() => {
    connect();

    return () => {
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
      }
      if (wsRef.current) {
        wsRef.current.close(1000, 'Component unmounted');
        wsRef.current = null;
      }
    };
  }, [connect]);

  // ─── Submit demand-ping ──────────────────────────────────────────────

  const submitDemandPing = useCallback((data: DemandPingRequest) => {
    if (!wsRef.current || wsRef.current.readyState !== WebSocket.OPEN) {
      setError('Not connected. Please wait for reconnection.');
      return;
    }

    const envelope: WsEnvelope<DemandPingRequest> = {
      action: 'demand-ping',
      requestId: generateRequestId(),
      data,
    };

    wsRef.current.send(JSON.stringify(envelope));
  }, []);

  // ─── Cancel demand ───────────────────────────────────────────────────

  const cancelDemand = useCallback(() => {
    if (!wsRef.current || wsRef.current.readyState !== WebSocket.OPEN) {
      setError('Not connected. Please wait for reconnection.');
      return;
    }

    const envelope: WsEnvelope<Record<string, never>> = {
      action: 'cancel-demand',
      requestId: generateRequestId(),
      data: {},
    };

    wsRef.current.send(JSON.stringify(envelope));
  }, []);

  return {
    status,
    activePing,
    submitDemandPing,
    cancelDemand,
    error,
  };
}
