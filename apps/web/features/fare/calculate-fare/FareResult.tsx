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
    <section aria-labelledby="fare-result-heading">
      <h2 id="fare-result-heading" className="mb-4 text-sm font-bold uppercase tracking-widest text-white/40">
        Fare Estimate
      </h2>

      {/* Big fare display */}
      <div className="mb-4 flex items-baseline justify-between rounded-xl border border-emerald-500/20 bg-emerald-500/10 px-5 py-4">
        <span className="text-sm font-medium text-emerald-400">Total Fare</span>
        <span className="text-3xl font-bold text-emerald-400">{formatPhp(result.amountPhp)}</span>
      </div>

      <dl className="space-y-3">
        <div className="flex items-center justify-between rounded-xl border border-white/10 bg-white/5 px-4 py-3">
          <dt className="text-sm text-white/60">Distance</dt>
          <dd className="text-sm font-semibold text-white">{result.distanceKm.toFixed(2)} km</dd>
        </div>
        <div className="flex items-center justify-between rounded-xl border border-white/10 bg-white/5 px-4 py-3">
          <dt className="text-sm text-white/60">Matrix Version</dt>
          <dd className="text-sm font-semibold text-white">{result.matrixVersion}</dd>
        </div>
      </dl>
    </section>
  );
}
