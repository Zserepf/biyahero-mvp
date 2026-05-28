'use client';

/**
 * Fare calculator page — route target composing the form and results panel.
 *
 * Anonymous access (no auth required for fare calculation).
 * Requirements: 2.1, 2.5, 2.9
 */

import { CalculateFareForm } from './CalculateFareForm';

export function CalculateFarePage() {
  return (
    <main className="mx-auto max-w-lg px-4 py-6">
      <h1 className="mb-2 text-2xl font-bold text-gray-900">
        Fare Calculator
      </h1>
      <p className="mb-6 text-sm text-gray-600">
        Compute the exact legally correct fare between any two points. Based on
        the official LTFRB fare matrix.
      </p>

      <CalculateFareForm />
    </main>
  );
}
