/**
 * Route feature types — mirrors the backend Routing_Service request/response shapes.
 *
 * These types are feature-scoped and evolve independently from other features.
 */

// ─── Enums ───────────────────────────────────────────────────────────────────

export type VehicleType = 'jeepney' | 'uv_express' | 'bus' | 'tricycle' | 'walk';

export type RouteStatus = 'unverified' | 'verified';

export type VoteKind = 'still_accurate' | 'no_longer_accurate';

export type RevisionStatus = 'pending' | 'approved' | 'rejected';

// ─── Waypoint ────────────────────────────────────────────────────────────────

export interface Waypoint {
  id?: string;
  lat: number;
  lng: number;
  position: number;
  name?: string;
}

// ─── Route ───────────────────────────────────────────────────────────────────

export interface RouteDto {
  id: string;
  ownerId: string;
  name: string;
  vehicleType: VehicleType;
  baseFare: number;
  status: RouteStatus;
  waypoints: Waypoint[];
  voteCounts: {
    stillAccurate: number;
    noLongerAccurate: number;
  };
  createdAt: string;
  updatedAt: string;
}

// ─── Create Route ────────────────────────────────────────────────────────────

export interface CreateRouteRequest {
  name: string;
  vehicleType: VehicleType;
  baseFare: number;
  waypoints: Omit<Waypoint, 'id'>[];
}

export interface CreateRouteResponse extends RouteDto {}

// ─── Route List (Bbox Query) ─────────────────────────────────────────────────

export interface BboxQuery {
  bboxSwLat: number;
  bboxSwLng: number;
  bboxNeLat: number;
  bboxNeLng: number;
}

export interface RouteListResponse {
  routes: RouteDto[];
}

// ─── Route Revision ──────────────────────────────────────────────────────────

export interface CreateRevisionRequest {
  waypoints: Omit<Waypoint, 'id'>[];
}

export interface RevisionDto {
  id: string;
  routeId: string;
  submitterId: string;
  status: RevisionStatus;
  waypoints: Waypoint[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateRevisionResponse extends RevisionDto {}

// ─── Route Vote ──────────────────────────────────────────────────────────────

export interface CastVoteRequest {
  kind: VoteKind;
}

export interface CastVoteResponse {
  id: string;
  routeId: string;
  voterId: string;
  kind: VoteKind;
  createdAt: string;
}
