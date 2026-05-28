/**
 * Fare calculator feature slice — public API.
 */

export { CalculateFarePage } from './CalculateFarePage';
export { CalculateFareForm } from './CalculateFareForm';
export { FareResult } from './FareResult';
export { useCalculateFare } from './useCalculateFare';
export { fareCalculateSchema, VEHICLE_TYPES, DISCOUNT_CATEGORIES } from './schema';
export type {
  FareCalculateRequest,
  FareCalculateResponse,
  VehicleType,
  DiscountCategory,
  Coordinate,
} from './types';
