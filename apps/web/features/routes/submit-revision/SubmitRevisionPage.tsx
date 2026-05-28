'use client';

/**
 * SubmitRevisionPage — Page component for editing an existing route.
 *
 * Route target for /routes/{id}/edit. Thin page that composes the revision form.
 * Requirements: 1.3
 */

import { useState, useCallback } from 'react';
import { SubmitRevisionForm } from './SubmitRevisionForm';

interface SubmitRevisionPageProps {
  routeId: string;
}

export function SubmitRevisionPage({ routeId }: SubmitRevisionPageProps) {
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
            Revision submitted!
          </p>
          <p className="text-sm text-green-600">
            Your edit has been saved as a pending revision. A moderator will review it.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4 p-4">
      <h1 className="text-xl font-bold text-gray-900">Edit Route</h1>
      <SubmitRevisionForm routeId={routeId} onSuccess={handleSuccess} />
    </div>
  );
}
