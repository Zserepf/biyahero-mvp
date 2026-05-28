/**
 * Fare calculator feature types — mirrors the backend Fare_Calculator request/response shapes.
 *
 * These types are feature-scoped and evolve independently from other features.
 * Requirements: 2.1, 2.5, 2.9
 */

// ─── Enums ───────────────────────────────────────────────────────────────────

export type VehicleType = 'Jeepney' | 'Bus' | 'UV_Express' | 'Tricycle';

export type DiscountCategory = 'regular' | 'student' | 'senior' | 'pwd';

// ─── Coordinate ──────────────────────────────────────────────────────────────

export interface Coordinate {
  lat: number;
  lng: number;
}

// ─── Request ─────────────────────────────────────────────────────────────────

export interface FareCalculateRequest {
  origin: Coordinate;
  destination: Coordinate;
  vehicleType: VehicleType;
  discountCategory?: DiscountCategory;
}

// ─── Response ────────────────────────────────────────────────────────────────

export interface FareCalculateResponse {
  amountPhp: number;
  distanceKm: number;
  matrixVersion: string;
}
