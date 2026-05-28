'use client';

/**
 * SubmitRevisionForm — Form for editing an existing route as a new revision.
 *
 * Loads the current route waypoints and allows the user to add/remove/reorder
 * waypoints. Submits as a pending revision linked to the original route.
 *
 * Requirements: 1.3, 6.4
 */

import { useState, useCallback, useEffect } from 'react';
import { createRevisionSchema, PH_LAT_MIN, PH_LAT_MAX, PH_LNG_MIN, PH_LNG_MAX } from '../schema';
import { useSubmitRevision } from './useSubmitRevision';
import { useRouteDetail } from '../route-detail/useRouteDetail';
import { RouteMap } from '../components/RouteMap';
import type { Waypoint, CreateRevisionRequest } from '../types';

interface SubmitRevisionFormProps {
  routeId: string;
  onSuccess?: () => void;
}

export function SubmitRevisionForm({ routeId, onSuccess }: SubmitRevisionFormProps) {
  const { data: route, isLoading: routeLoading } = useRouteDetail(routeId);
  const submitRevision = useSubmitRevision(routeId);

  const [waypoints, setWaypoints] = useState<Waypoint[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [initialized, setInitialized] = useState(false);

  // Initialize waypoints from the existing route
  useEffect(() => {
    if (route && !initialized) {
      setWaypoints(route.waypoints);
      setInitialized(true);
    }
  }, [route, initialized]);

  const handleWaypointAdd = useCallback((lat: number, lng: number) => {
    if (lat < PH_LAT_MIN || lat > PH_LAT_MAX || lng < PH_LNG_MIN || lng > PH_LNG_MAX) {
      setErrors((prev) => ({
        ...prev,
        waypoints: 'routes.waypointOutsidePhilippines',
      }));
      return;
    }

    setWaypoints((prev) => [
      ...prev,
      {
        lat,
        lng,
        position: prev.length,
        name: undefined,
      },
    ]);
    setErrors((prev) => {
      const { waypoints: _, ...rest } = prev;
      return rest;
    });
  }, []);

  const handleRemoveWaypoint = useCallback((index: number) => {
    setWaypoints((prev) =>
      prev
        .filter((_, i) => i !== index)
        .map((wp, i) => ({ ...wp, position: i })),
    );
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrors({});

    const formData = {
      waypoints: waypoints.map((wp) => ({
        lat: wp.lat,
        lng: wp.lng,
        position: wp.position,
        name: wp.name,
      })),
    };

    const result = createRevisionSchema.safeParse(formData);

    if (!result.success) {
      const fieldErrors: Record<string, string> = {};
      for (const issue of result.error.issues) {
        const path = issue.path.join('.');
        fieldErrors[path] = issue.message;
      }
      setErrors(fieldErrors);
      return;
    }

    const request: CreateRevisionRequest = result.data;

    try {
      await submitRevision.mutateAsync(request);
      onSuccess?.();
    } catch {
      setErrors({ form: 'routes.revisionSubmitFailed' });
    }
  };

  if (routeLoading) {
    return (
      <div className="flex items-center justify-center p-8">
        <p className="text-sm text-gray-500 dark:text-white/40">Loading route data...</p>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <p className="text-sm text-gray-600 dark:text-white/60">
        Edit the waypoints below. Your changes will be submitted as a pending revision
        for community review.
      </p>

      {/* Map for editing waypoints */}
      <RouteMap
        waypoints={waypoints}
        editable={true}
        onWaypointAdd={handleWaypointAdd}
        height="350px"
      />
      {errors.waypoints && (
        <p className="text-sm text-red-600 dark:text-red-400" role="alert">
          {errors.waypoints}
        </p>
      )}

      {/* Waypoint list */}
      {waypoints.length > 0 && (
        <div className="flex flex-col gap-2">
          <p className="text-sm font-medium text-gray-700 dark:text-white/80">
            Waypoints ({waypoints.length})
          </p>
          <ul className="flex flex-col gap-1" aria-label="Revision waypoints">
            {waypoints.map((wp, index) => (
              <li
                key={`rev-wp-${index}`}
                className="flex items-center justify-between rounded-md bg-gray-50 dark:bg-white/5 px-3 py-2 text-sm text-gray-800 dark:text-white/70"
              >
                <span>
                  {wp.name || `Waypoint ${index + 1}`} — {wp.lat.toFixed(5)},{' '}
                  {wp.lng.toFixed(5)}
                </span>
                <button
                  type="button"
                  onClick={() => handleRemoveWaypoint(index)}
                  className="min-h-[44px] min-w-[44px] rounded-md text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 focus:outline-none focus:ring-2 focus:ring-red-200 dark:focus:ring-red-500/30"
                  aria-label={`Remove waypoint ${index + 1}`}
                >
                  ✕
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Form-level error */}
      {errors.form && (
        <p className="text-sm text-red-600 dark:text-red-400" role="alert">
          {errors.form}
        </p>
      )}

      {/* Submit button */}
      <button
        type="submit"
        disabled={submitRevision.isPending || waypoints.length < 2}
        className="min-h-[44px] rounded-lg bg-blue-600 px-4 py-3 text-base font-semibold text-white transition-colors hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-300 dark:focus:ring-blue-500/40 disabled:cursor-not-allowed disabled:bg-gray-400"
        aria-label="Submit revision"
      >
        {submitRevision.isPending ? 'Submitting...' : 'Submit Revision'}
      </button>
    </form>
  );
}
