/**
 * Types for the Driver Heatmap feature slice.
 *
 * NEVER includes commuter identity (no names, IDs, or personal info).
 * Only: geohash7 location, demand count, vehicle type.
 *
 * Requirements: 4.2, 4.3, 4.6
 */

/** Supported vehicle types matching the backend enum */
export type VehicleType = 'jeepney' | 'uv_express' | 'bus';

/**
 * A single heatmap tile representing aggregated demand in a geohash7 cell.
 * Contains NO personally identifying information — only spatial demand data.
 */
export interface HeatmapTile {
  /** Geohash precision-7 string (~150m cell) */
  geohash7: string;
  /** Number of active demand pings in this cell */
  demandCount: number;
  /** Vehicle type for this demand bucket */
  vehicleType: VehicleType;
}

/** Bounding box for heatmap subscription */
export interface Bbox {
  swLat: number;
  swLng: number;
  neLat: number;
  neLng: number;
}

/** Request payload for subscribing to heatmap updates */
export interface SubscribeHeatmapRequest {
  action: 'subscribe-heatmap';
  requestId: string;
  data: {
    bbox: Bbox;
  };
}

/** Server-pushed heatmap delta event envelope */
export interface HeatmapDeltaEvent {
  action: 'heatmap.delta';
  requestId: string;
  data: {
    tiles: HeatmapTile[];
  };
  emittedAt: string;
}

/** WebSocket connection states */
export type WsConnectionStatus = 'connecting' | 'connected' | 'disconnected' | 'error';
