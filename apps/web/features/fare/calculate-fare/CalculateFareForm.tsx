'use client';

/**
 * Fare calculator form — origin/destination picker, vehicle type dropdown,
 * optional discount category select.
 *
 * Uses Zod for client-side validation before calling the API.
 * Anonymous access (no auth required).
 *
 * Requirements: 2.1, 2.5, 2.9
 */

import { useState, type FormEvent } from 'react';
import {
  fareCalculateSchema,
  VEHICLE_TYPES,
  DISCOUNT_CATEGORIES,
} from './schema';
import { useCalculateFare } from './useCalculateFare';
import { FareResult } from './FareResult';
import type { VehicleType, DiscountCategory } from './types';

// ─── Vehicle type display labels ─────────────────────────────────────────────

const VEHICLE_TYPE_LABELS: Record<VehicleType, string> = {
  Jeepney: 'Jeepney',
  Bus: 'Bus',
  UV_Express: 'UV Express',
  Tricycle: 'Tricycle',
};

const DISCOUNT_CATEGORY_LABELS: Record<DiscountCategory, string> = {
  regular: 'Regular',
  student: 'Student (20% off)',
  senior: 'Senior Citizen (20% off)',
  pwd: 'PWD (20% off)',
};

// ─── Form Field Errors ───────────────────────────────────────────────────────

interface FieldErrors {
  originLat?: string;
  originLng?: string;
  destinationLat?: string;
  destinationLng?: string;
  vehicleType?: string;
}

export function CalculateFareForm() {
  // Form state
  const [originLat, setOriginLat] = useState('');
  const [originLng, setOriginLng] = useState('');
  const [destinationLat, setDestinationLat] = useState('');
  const [destinationLng, setDestinationLng] = useState('');
  const [vehicleType, setVehicleType] = useState<VehicleType | ''>('');
  const [discountCategory, setDiscountCategory] =
    useState<DiscountCategory>('regular');

  // Validation errors
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  // Hook
  const { calculateFare, result, isLoading, error } = useCalculateFare();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setFieldErrors({});

    // Parse numeric inputs
    const originLatNum = parseFloat(originLat);
    const originLngNum = parseFloat(originLng);
    const destLatNum = parseFloat(destinationLat);
    const destLngNum = parseFloat(destinationLng);

    // Build the raw form data for Zod validation
    const rawData = {
      origin: { lat: originLatNum, lng: originLngNum },
      destination: { lat: destLatNum, lng: destLngNum },
      vehicleType: vehicleType || undefined,
      discountCategory: discountCategory,
    };

    // Validate with Zod
    const parsed = fareCalculateSchema.safeParse(rawData);

    if (!parsed.success) {
      const errors: FieldErrors = {};
      for (const issue of parsed.error.issues) {
        const path = issue.path.join('.');
        if (path === 'origin.lat') errors.originLat = issue.message;
        if (path === 'origin.lng') errors.originLng = issue.message;
        if (path === 'destination.lat') errors.destinationLat = issue.message;
        if (path === 'destination.lng') errors.destinationLng = issue.message;
        if (path === 'vehicleType') errors.vehicleType = issue.message;
      }
      setFieldErrors(errors);
      return;
    }

    // Call the API
    try {
      await calculateFare(parsed.data);
    } catch {
      // Error is already captured in the hook state
    }
  };

  return (
    <div>
      <form onSubmit={handleSubmit} noValidate aria-label="Fare calculator form">
        {/* Origin Coordinates */}
        <fieldset className="mb-4">
          <legend className="mb-2 text-sm font-semibold text-gray-700">
            Origin
          </legend>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label
                htmlFor="origin-lat"
                className="mb-1 block text-xs font-medium text-gray-600"
              >
                Latitude
              </label>
              <input
                id="origin-lat"
                type="number"
                step="any"
                inputMode="decimal"
                placeholder="e.g. 14.5995"
                value={originLat}
                onChange={(e) => setOriginLat(e.target.value)}
                aria-invalid={!!fieldErrors.originLat}
                aria-describedby={
                  fieldErrors.originLat ? 'origin-lat-error' : undefined
                }
                className="min-h-[44px] w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
              />
              {fieldErrors.originLat && (
                <p
                  id="origin-lat-error"
                  className="mt-1 text-xs text-red-600"
                  role="alert"
                >
                  {fieldErrors.originLat}
                </p>
              )}
            </div>
            <div>
              <label
                htmlFor="origin-lng"
                className="mb-1 block text-xs font-medium text-gray-600"
              >
                Longitude
              </label>
              <input
                id="origin-lng"
                type="number"
                step="any"
                inputMode="decimal"
                placeholder="e.g. 120.9842"
                value={originLng}
                onChange={(e) => setOriginLng(e.target.value)}
                aria-invalid={!!fieldErrors.originLng}
                aria-describedby={
                  fieldErrors.originLng ? 'origin-lng-error' : undefined
                }
                className="min-h-[44px] w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
              />
              {fieldErrors.originLng && (
                <p
                  id="origin-lng-error"
                  className="mt-1 text-xs text-red-600"
                  role="alert"
                >
                  {fieldErrors.originLng}
                </p>
              )}
            </div>
          </div>
        </fieldset>

        {/* Destination Coordinates */}
        <fieldset className="mb-4">
          <legend className="mb-2 text-sm font-semibold text-gray-700">
            Destination
          </legend>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label
                htmlFor="dest-lat"
                className="mb-1 block text-xs font-medium text-gray-600"
              >
                Latitude
              </label>
              <input
                id="dest-lat"
                type="number"
                step="any"
                inputMode="decimal"
                placeholder="e.g. 14.5547"
                value={destinationLat}
                onChange={(e) => setDestinationLat(e.target.value)}
                aria-invalid={!!fieldErrors.destinationLat}
                aria-describedby={
                  fieldErrors.destinationLat ? 'dest-lat-error' : undefined
                }
                className="min-h-[44px] w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
              />
              {fieldErrors.destinationLat && (
                <p
                  id="dest-lat-error"
                  className="mt-1 text-xs text-red-600"
                  role="alert"
                >
                  {fieldErrors.destinationLat}
                </p>
              )}
            </div>
            <div>
              <label
                htmlFor="dest-lng"
                className="mb-1 block text-xs font-medium text-gray-600"
              >
                Longitude
              </label>
              <input
                id="dest-lng"
                type="number"
                step="any"
                inputMode="decimal"
                placeholder="e.g. 121.0244"
                value={destinationLng}
                onChange={(e) => setDestinationLng(e.target.value)}
                aria-invalid={!!fieldErrors.destinationLng}
                aria-describedby={
                  fieldErrors.destinationLng ? 'dest-lng-error' : undefined
                }
                className="min-h-[44px] w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
              />
              {fieldErrors.destinationLng && (
                <p
                  id="dest-lng-error"
                  className="mt-1 text-xs text-red-600"
                  role="alert"
                >
                  {fieldErrors.destinationLng}
                </p>
              )}
            </div>
          </div>
        </fieldset>

        {/* Vehicle Type */}
        <div className="mb-4">
          <label
            htmlFor="vehicle-type"
            className="mb-1 block text-sm font-semibold text-gray-700"
          >
            Vehicle Type
          </label>
          <select
            id="vehicle-type"
            value={vehicleType}
            onChange={(e) => setVehicleType(e.target.value as VehicleType)}
            aria-invalid={!!fieldErrors.vehicleType}
            aria-describedby={
              fieldErrors.vehicleType ? 'vehicle-type-error' : undefined
            }
            className="min-h-[44px] w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
          >
            <option value="">Select vehicle type</option>
            {VEHICLE_TYPES.map((type) => (
              <option key={type} value={type}>
                {VEHICLE_TYPE_LABELS[type]}
              </option>
            ))}
          </select>
          {fieldErrors.vehicleType && (
            <p
              id="vehicle-type-error"
              className="mt-1 text-xs text-red-600"
              role="alert"
            >
              {fieldErrors.vehicleType}
            </p>
          )}
        </div>

        {/* Discount Category (optional) */}
        <div className="mb-6">
          <label
            htmlFor="discount-category"
            className="mb-1 block text-sm font-semibold text-gray-700"
          >
            Discount Category
          </label>
          <select
            id="discount-category"
            value={discountCategory}
            onChange={(e) =>
              setDiscountCategory(e.target.value as DiscountCategory)
            }
            className="min-h-[44px] w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
          >
            {DISCOUNT_CATEGORIES.map((cat) => (
              <option key={cat} value={cat}>
                {DISCOUNT_CATEGORY_LABELS[cat]}
              </option>
            ))}
          </select>
        </div>

        {/* Submit Button */}
        <button
          type="submit"
          disabled={isLoading}
          className="min-h-[44px] w-full rounded-md bg-blue-600 px-4 py-3 text-sm font-semibold text-white transition-colors hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {isLoading ? 'Calculating...' : 'Calculate Fare'}
        </button>

        {/* API Error */}
        {error && (
          <p className="mt-3 text-sm text-red-600" role="alert">
            {error}
          </p>
        )}
      </form>

      {/* Results Panel */}
      {result && <FareResult result={result} />}
    </div>
  );
}
