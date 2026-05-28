'use client';

/**
 * CreateRoutePage — Page component for creating a new route.
 *
 * Route target for /routes/create. Thin page that composes the form.
 * Requirements: 1.1
 */

import { useCallback, useState } from 'react';
import { CreateRouteForm } from './CreateRouteForm';

export function CreateRoutePage() {
  const [submitted, setSubmitted] = useState(false);

  const handleSuccess = useCallback(() => {
    setSubmitted(true);
  }, []);

  if (submitted) {
    return (
      <div className="flex flex-col items-center gap-4 p-6">
        <div
          className="rounded-lg bg-green-50 p-4 text-center"
          role="status"
          aria-live="polite"
        >
          <p className="text-lg font-semibold text-green-800">
            Route submitted successfully!
          </p>
          <p className="text-sm text-green-600">
            Your route has been saved with status &quot;unverified&quot; and will be
            reviewed by the community.
          </p>
        </div>
        <button
          onClick={() => setSubmitted(false)}
          className="min-h-[44px] rounded-lg bg-blue-600 px-4 py-2 text-base font-medium text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-300"
        >
          Plot Another Route
        </button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4 p-4">
      <h1 className="text-xl font-bold text-gray-900">Create New Route</h1>
      <p className="text-sm text-gray-600">
        Plot at least 2 waypoints on the map to define your route. Tap the map to add
        waypoints in order.
      </p>
      <CreateRouteForm onSuccess={handleSuccess} />
    </div>
  );
}
