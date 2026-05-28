/**
 * Zod validation schema for the fare calculator form.
 *
 * Client-side validation for instant UX feedback.
 * The backend remains the source of truth for business rules (haversine, matrix lookup).
 * Requirements: 2.1, 2.5
 */

import { z } from 'zod';

// ─── Constants ───────────────────────────────────────────────────────────────

const VEHICLE_TYPES = ['Jeepney', 'Bus', 'UV_Express', 'Tricycle'] as const;
const DISCOUNT_CATEGORIES = ['regular', 'student', 'senior', 'pwd'] as const;

// Philippines bounding box (approximate)
const PH_LAT_MIN = 4.5;
const PH_LAT_MAX = 21.5;
const PH_LNG_MIN = 116.0;
const PH_LNG_MAX = 127.0;

// ─── Coordinate Schema ───────────────────────────────────────────────────────

const coordinateSchema = z.object({
  lat: z
    .number({ message: 'fare.originRequired' })
    .min(PH_LAT_MIN, 'fare.coordinateOutOfRange')
    .max(PH_LAT_MAX, 'fare.coordinateOutOfRange'),
  lng: z
    .number({ message: 'fare.originRequired' })
    .min(PH_LNG_MIN, 'fare.coordinateOutOfRange')
    .max(PH_LNG_MAX, 'fare.coordinateOutOfRange'),
});

// ─── Fare Calculate Schema ───────────────────────────────────────────────────

export const fareCalculateSchema = z.object({
  origin: coordinateSchema,
  destination: coordinateSchema,
  vehicleType: z.enum(VEHICLE_TYPES, {
    message: 'fare.vehicleTypeRequired',
  }),
  discountCategory: z.enum(DISCOUNT_CATEGORIES).optional().default('regular'),
});

export type FareCalculateFormData = z.infer<typeof fareCalculateSchema>;

export { VEHICLE_TYPES, DISCOUNT_CATEGORIES };
