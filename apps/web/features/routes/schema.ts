/**
 * Zod validation schemas for route forms.
 *
 * Client-side validation for instant UX feedback.
 * Validates: ≥2 waypoints, Philippines bbox coordinates (lat 4.5°–21.5° N, lng 116°–127° E).
 * Requirements: 1.1, 1.7, 1.8
 */

import { z } from 'zod';

// ─── Philippines Bounding Box ────────────────────────────────────────────────

const PH_LAT_MIN = 4.5;
const PH_LAT_MAX = 21.5;
const PH_LNG_MIN = 116.0;
const PH_LNG_MAX = 127.0;

// ─── Waypoint Schema ─────────────────────────────────────────────────────────

export const waypointSchema = z.object({
  lat: z
    .number()
    .min(PH_LAT_MIN, 'routes.waypointLatOutOfRange')
    .max(PH_LAT_MAX, 'routes.waypointLatOutOfRange'),
  lng: z
    .number()
    .min(PH_LNG_MIN, 'routes.waypointLngOutOfRange')
    .max(PH_LNG_MAX, 'routes.waypointLngOutOfRange'),
  position: z.number().int().min(0),
  name: z.string().optional(),
});

export type WaypointFormData = z.infer<typeof waypointSchema>;

// ─── Create Route Schema ─────────────────────────────────────────────────────

export const createRouteSchema = z.object({
  name: z
    .string()
    .min(1, 'forms.required')
    .max(200, 'routes.nameTooLong'),
  vehicleType: z.enum(['jeepney', 'uv_express', 'bus', 'tricycle', 'walk'], {
    message: 'forms.required',
  }),
  baseFare: z
    .number()
    .min(0, 'routes.baseFareNegative'),
  waypoints: z
    .array(waypointSchema)
    .min(2, 'routes.minTwoWaypoints'),
});

export type CreateRouteFormData = z.infer<typeof createRouteSchema>;

// ─── Create Revision Schema ──────────────────────────────────────────────────

export const createRevisionSchema = z.object({
  waypoints: z
    .array(waypointSchema)
    .min(2, 'routes.minTwoWaypoints'),
});

export type CreateRevisionFormData = z.infer<typeof createRevisionSchema>;

// ─── Vote Schema ─────────────────────────────────────────────────────────────

export const castVoteSchema = z.object({
  kind: z.enum(['still_accurate', 'no_longer_accurate'], {
    message: 'forms.required',
  }),
});

export type CastVoteFormData = z.infer<typeof castVoteSchema>;

export { PH_LAT_MIN, PH_LAT_MAX, PH_LNG_MIN, PH_LNG_MAX };
