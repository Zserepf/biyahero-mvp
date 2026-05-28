'use client';

/**
 * useDriverHeatmap — WebSocket hook that subscribes to heatmap with bbox,
 * receives heatmap.delta events, and maintains real-time tile state.
 *
 * - Subscribes with the current map bbox
 * - Re-subscribes on pan/zoom (bbox change)
 * - Never exposes commuter identity
 *
 * Requirements: 4.2, 4.3, 4.6
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { env } from '@/infrastructure/config/env';
import type {
  Bbox,
  HeatmapDeltaEvent,
  HeatmapTile,
  SubscribeHeatmapRequest,
  WsConnectionStatus,
} from './types';

interface UseDriverHeatmapOptions {
  /** Whether the hook should connect (e.g., only when the page is active) */
  enabled?: boolean;
}

interface UseDriverHeatmapReturn {
  /** Current heatmap tiles keyed by geohash7 */
  tiles: HeatmapTile[];
  /** WebSocket connection status */
  status: WsConnectionStatus;
  /** Subscribe to a new bounding box (call on pan/zoom) */
  subscribeToBbox: (bbox: Bbox) => void;
  /** Manually disconnect */
  disconnect: () => void;
}

/**
 * Generate a simple UUID v4 for request IDs.
 */
function generateRequestId(): string {
  return crypto.randomUUID();
}

/**
 * Read the JWT access token from localStorage for WebSocket auth.
 * Anonymous subscribe is permitted per Req 4.7, so token may be null.
 */
function getAccessToken(): string | null {
  if (typeof window === 'undefined') return null;
  return localStorage.getItem('biyahero_access_token');
}

export function useDriverHeatmap(
  options: UseDriverHeatmapOptions = {},
): UseDriverHeatmapReturn {
  const { enabled = true } = options;

  const [tiles, setTiles] = useState<HeatmapTile[]>([]);
  const [status, setStatus] = useState<WsConnectionStatus>('disconnected');

  const wsRef = useRef<WebSocket | null>(null);
  const currentBboxRef = useRef<Bbox | null>(null);
  const reconnectTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  /**
   * Build the WebSocket URL with optional JWT token query param.
   * Anonymous subscribe is allowed for read-only heatmap streams (Req 4.7).
   */
  const buildWsUrl = useCallback((): string => {
    const token = getAccessToken();
    const baseUrl = env.WS_URL;
    if (token) {
      return `${baseUrl}?token=${token}`;
    }
    return baseUrl;
  }, []);

  /**
   * Send a subscribe-heatmap message over the WebSocket.
   */
  const sendSubscription = useCallback((bbox: Bbox) => {
    const ws = wsRef.current;
    if (!ws || ws.readyState !== WebSocket.OPEN) return;

    const message: SubscribeHeatmapRequest = {
      action: 'subscribe-heatmap',
      requestId: generateRequestId(),
      data: { bbox },
    };

    ws.send(JSON.stringify(message));
  }, []);

  /**
   * Connect to the WebSocket and set up event handlers.
   */
  const connect = useCallback(() => {
    if (wsRef.current?.readyState === WebSocket.OPEN) return;
    if (wsRef.current?.readyState === WebSocket.CONNECTING) return;

    setStatus('connecting');

    const ws = new WebSocket(buildWsUrl());
    wsRef.current = ws;

    ws.onopen = () => {
      setStatus('connected');

      // Re-subscribe to the current bbox if one was set before connection
      if (currentBboxRef.current) {
        sendSubscription(currentBboxRef.current);
      }
    };

    ws.onmessage = (event: MessageEvent) => {
      try {
        const message = JSON.parse(event.data as string);

        if (message.action === 'heatmap.delta') {
          const delta = message as HeatmapDeltaEvent;
          const incomingTiles = delta.data.tiles;

          // Replace tiles state with the latest delta from the server.
          // The server sends the full set of active tiles for the subscribed bbox.
          setTiles((prevTiles) => {
            const tileMap = new Map<string, HeatmapTile>();

            // Keep existing tiles
            for (const tile of prevTiles) {
              tileMap.set(tile.geohash7, tile);
            }

            // Apply delta — update or add tiles
            for (const tile of incomingTiles) {
              if (tile.demandCount > 0) {
                tileMap.set(tile.geohash7, tile);
              } else {
                // Remove tiles with zero demand
                tileMap.delete(tile.geohash7);
              }
            }

            return Array.from(tileMap.values());
          });
        }
      } catch {
        // Ignore malformed messages
      }
    };

    ws.onclose = () => {
      setStatus('disconnected');
      wsRef.current = null;

      // Auto-reconnect after 3 seconds if still enabled
      if (enabled) {
        reconnectTimeoutRef.current = setTimeout(() => {
          connect();
        }, 3000);
      }
    };

    ws.onerror = () => {
      setStatus('error');
    };
  }, [buildWsUrl, enabled, sendSubscription]);

  /**
   * Subscribe to a new bounding box. Called on map pan/zoom.
   * Re-subscribes immediately if connected, or stores for when connection opens.
   */
  const subscribeToBbox = useCallback(
    (bbox: Bbox) => {
      currentBboxRef.current = bbox;

      // Clear existing tiles when bbox changes (new viewport)
      setTiles([]);

      // Send subscription if already connected
      sendSubscription(bbox);
    },
    [sendSubscription],
  );

  /**
   * Manually disconnect the WebSocket.
   */
  const disconnect = useCallback(() => {
    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }

    if (wsRef.current) {
      wsRef.current.close();
      wsRef.current = null;
    }

    setStatus('disconnected');
    setTiles([]);
  }, []);

  // Connect on mount (if enabled), disconnect on unmount
  useEffect(() => {
    if (enabled) {
      connect();
    }

    return () => {
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
      }
      if (wsRef.current) {
        wsRef.current.close();
        wsRef.current = null;
      }
    };
  }, [enabled, connect]);

  return {
    tiles,
    status,
    subscribeToBbox,
    disconnect,
  };
}
