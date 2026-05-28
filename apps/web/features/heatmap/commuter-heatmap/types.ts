/**
 * Commuter heatmap feature types — mirrors the backend WebSocket protocol
 * for demand-ping submission and cancellation.
 *
 * These types are feature-scoped and evolve independently from other features.
 * Requirements: 4.1, 4.5
 */

// ─── Enums ───────────────────────────────────────────────────────────────────

export type VehicleType = 'jeepney' | 'uv_express' | 'bus';

// ─── WebSocket Envelope ──────────────────────────────────────────────────────

export interface WsEnvelope<T = unknown> {
  action: string;
  requestId: string;
  data: T;
  emittedAt?: string;
}

// ─── Demand Ping ─────────────────────────────────────────────────────────────

export interface DemandPingRequest {
  lat: number;
  lng: number;
  vehicleType: VehicleType;
}

export interface DemandPingResponse {
  pingId: string;
  lat: number;
  lng: number;
  vehicleType: VehicleType;
  expiresAt: string;
}

// ─── Cancel Demand ───────────────────────────────────────────────────────────

export interface CancelDemandResponse {
  cancelled: boolean;
}

// ─── Connection State ────────────────────────────────────────────────────────

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error';
