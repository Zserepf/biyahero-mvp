'use client';

/**
 * Fare calculator page — map-based origin/destination picker with fare calculation.
 *
 * Users click the map to set origin (first click) and destination (second click),
 * then select vehicle type and discount category to calculate the fare.
 *
 * Anonymous access (no auth required for fare calculation).
 * Requirements: 2.1, 2.5, 2.9
 */

import { useState, type FormEvent } from 'react';
import dynamic from 'next/dynamic';
import {
  fareCalculateSchema,
  VEHICLE_TYPES,
  DISCOUNT_CATEGORIES,
} from './schema';
import { useCalculateFare } from './useCalculateFare';
import { FareResult } from './FareResult';
import type { VehicleType, DiscountCategory, Coordinate } from './types';

// Dynamic import for Leaflet map (no SSR)
const FareMapPicker = dynamic(
  () => import('./FareMapPicker').then((mod) => mod.FareMapPicker),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-[360px] w-full items-center justify-center rounded-xl border border-gray-200 bg-gray-50">
        <p className="text-sm text-gray-500">Loading map...</p>
      </div>
    ),
  },
);

// ─── Labels ──────────────────────────────────────────────────────────────────

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

export function CalculateFarePage() {
  // Map pin state
  const [origin, setOrigin] = useState<Coordinate | null>(null);
  const [destination, setDestination] = useState<Coordinate | null>(null);

  // Form state
  const [vehicleType, setVehicleType] = useState<VehicleType | ''>('');
  const [discountCategory, setDiscountCategory] = useState<DiscountCategory>('regular');

  // Validation
  const [formError, setFormError] = useState<string | null>(null);

  // Hook
  const { calculateFare, result, isLoading, error } = useCalculateFare();

  const handleReset = () => {
    setOrigin(null);
    setDestination(null);
    setFormError(null);
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setFormError(null);

    if (!origin) {
      setFormError('Please tap the map to set your origin point.');
      return;
    }
    if (!destination) {
      setFormError('Please tap the map again to set your destination.');
      return;
    }
    if (!vehicleType) {
      setFormError('Please select a vehicle type.');
      return;
    }

    const rawData = {
      origin,
      destination,
      vehicleType,
      discountCategory,
    };

    const parsed = fareCalculateSchema.safeParse(rawData);

    if (!parsed.success) {
      const firstIssue = parsed.error.issues[0];
      setFormError(firstIssue?.message || 'Invalid input. Please check your selections.');
      return;
    }

    try {
      await calculateFare(parsed.data);
    } catch {
      // Error captured in hook state
    }
  };

  return (
    <main className="mx-auto max-w-2xl px-4 py-6">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Fare Calculator</h1>
        <p className="mt-1 text-sm text-gray-600">
          Compute the exact legally correct fare between any two points. Based on
          the official LTFRB fare matrix.
        </p>
      </div>

      {/* Map Card */}
      <div className="mb-6 rounded-2xl bg-white p-4 shadow-md ring-1 ring-gray-100">
        {/* Instructions */}
        <div className="mb-3 flex items-center gap-2 rounded-lg bg-blue-50 px-3 py-2">
          <svg className="h-4 w-4 shrink-0 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <p className="text-xs text-blue-700">
            Tap the map to set your <span className="font-semibold text-green-700">origin</span>, then tap again for your <span className="font-semibold text-red-700">destination</span>.
          </p>
        </div>

        {/* Map */}
        <FareMapPicker
          origin={origin}
          destination={destination}
          onOriginChange={setOrigin}
          onDestinationChange={setDestination}
        />

        {/* Coordinates display + Reset */}
        <div className="mt-3 flex items-center justify-between">
          <div className="flex flex-wrap gap-3 text-xs text-gray-600">
            {origin && (
              <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-2 py-1 text-green-700">
                <span className="h-2 w-2 rounded-full bg-green-500" />
                {origin.lat.toFixed(4)}, {origin.lng.toFixed(4)}
              </span>
            )}
            {destination && (
              <span className="inline-flex items-center gap-1 rounded-full bg-red-50 px-2 py-1 text-red-700">
                <span className="h-2 w-2 rounded-full bg-red-500" />
                {destination.lat.toFixed(4)}, {destination.lng.toFixed(4)}
              </span>
            )}
            {!origin && !destination && (
              <span className="text-gray-400 italic">No pins set yet</span>
            )}
          </div>
          <button
            type="button"
            onClick={handleReset}
            className="rounded-md px-3 py-1.5 text-xs font-medium text-gray-600 transition-colors hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-blue-200"
          >
            Reset Pins
          </button>
        </div>
      </div>

      {/* Form Controls */}
      <form onSubmit={handleSubmit} noValidate aria-label="Fare calculator form">
        <div className="rounded-2xl bg-white p-4 shadow-md ring-1 ring-gray-100">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            {/* Vehicle Type */}
            <div>
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
                className="min-h-[44px] w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
              >
                <option value="">Select vehicle type</option>
                {VEHICLE_TYPES.map((type) => (
                  <option key={type} value={type}>
                    {VEHICLE_TYPE_LABELS[type]}
                  </option>
                ))}
              </select>
            </div>

            {/* Discount Category */}
            <div>
              <label
                htmlFor="discount-category"
                className="mb-1 block text-sm font-semibold text-gray-700"
              >
                Discount Category
              </label>
              <select
                id="discount-category"
                value={discountCategory}
                onChange={(e) => setDiscountCategory(e.target.value as DiscountCategory)}
                className="min-h-[44px] w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
              >
                {DISCOUNT_CATEGORIES.map((cat) => (
                  <option key={cat} value={cat}>
                    {DISCOUNT_CATEGORY_LABELS[cat]}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Error */}
          {(formError || error) && (
            <p className="mt-3 text-sm text-red-600" role="alert">
              {formError || error}
            </p>
          )}

          {/* Submit */}
          <button
            type="submit"
            disabled={isLoading}
            className="mt-4 min-h-[44px] w-full rounded-lg bg-blue-600 px-4 py-3 text-sm font-semibold text-white transition-colors hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isLoading ? 'Calculating...' : 'Calculate Fare'}
          </button>
        </div>
      </form>

      {/* Results */}
      {result && (
        <div className="mt-6">
          <FareResult result={result} />
        </div>
      )}
    </main>
  );
}
