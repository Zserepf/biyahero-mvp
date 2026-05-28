'use client';

/**
 * Results panel showing the computed fare, distance, and matrix version.
 *
 * Displays:
 * - amountPhp formatted as ₱XX.XX
 * - distanceKm in kilometers
 * - matrixVersion (LTFRB fare matrix version used)
 *
 * Requirements: 2.1, 2.9
 */

import type { FareCalculateResponse } from './types';

interface FareResultProps {
  result: FareCalculateResponse;
}

/**
 * Format a PHP amount to ₱XX.XX display format.
 */
function formatPhp(amount: number): string {
  return `₱${amount.toFixed(2)}`;
}

export function FareResult({ result }: FareResultProps) {
  return (
    <section
      aria-labelledby="fare-result-heading"
      className="mt-6 rounded-lg border border-green-200 bg-green-50 p-6"
    >
      <h2
        id="fare-result-heading"
        className="mb-4 text-lg font-semibold text-green-900"
      >
        Fare Estimate
      </h2>

      <dl className="space-y-3">
        {/* Fare Amount */}
        <div className="flex items-baseline justify-between">
          <dt className="text-sm font-medium text-green-700">Fare</dt>
          <dd className="text-2xl font-bold text-green-900">
            {formatPhp(result.amountPhp)}
          </dd>
        </div>

        {/* Distance */}
        <div className="flex items-baseline justify-between">
          <dt className="text-sm font-medium text-green-700">Distance</dt>
          <dd className="text-base font-medium text-green-800">
            {result.distanceKm.toFixed(2)} km
          </dd>
        </div>

        {/* Matrix Version */}
        <div className="flex items-baseline justify-between">
          <dt className="text-sm font-medium text-green-700">
            Matrix Version
          </dt>
          <dd className="text-sm text-green-600">{result.matrixVersion}</dd>
        </div>
      </dl>
    </section>
  );
}
