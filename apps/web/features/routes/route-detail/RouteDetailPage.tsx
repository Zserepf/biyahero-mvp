'use client';

/**
 * RouteDetailPage — Page component for viewing a single route.
 *
 * Displays the route on a map with waypoints, metadata, and vote counts.
 * Provides actions to vote or edit (submit revision).
 *
 * Requirements: 1.2, 1.5
 */

import { RouteMap } from '../components/RouteMap';
import { useRouteDetail } from './useRouteDetail';
import { VoteRoutePanel } from '../vote-route/VoteRoutePanel';

interface RouteDetailPageProps {
  routeId: string;
  onEditRoute?: (routeId: string) => void;
}

export function RouteDetailPage({ routeId, onEditRoute }: RouteDetailPageProps) {
  const { data: route, isLoading, error } = useRouteDetail(routeId);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-8">
        <p className="text-sm text-gray-500 dark:text-white/40" aria-live="polite">
          Loading route...
        </p>
      </div>
    );
  }

  if (error || !route) {
    return (
      <div className="flex items-center justify-center p-8">
        <p className="text-sm text-red-600 dark:text-red-400" role="alert">
          Failed to load route. Please try again.
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4 p-4">
      {/* Route header */}
      <div className="flex flex-col gap-1">
        <h1 className="text-xl font-bold text-gray-900 dark:text-white">{route.name}</h1>
        <div className="flex flex-wrap items-center gap-2 text-sm text-gray-600 dark:text-white/60">
          <span className="rounded-full bg-blue-100 dark:bg-blue-500/20 px-2 py-0.5 text-xs font-medium text-blue-800 dark:text-blue-300">
            {route.vehicleType}
          </span>
          <span
            className={`rounded-full px-2 py-0.5 text-xs font-medium ${
              route.status === 'verified'
                ? 'bg-green-100 dark:bg-green-500/20 text-green-800 dark:text-green-300'
                : 'bg-yellow-100 dark:bg-yellow-500/20 text-yellow-800 dark:text-yellow-300'
            }`}
          >
            {route.status}
          </span>
          <span>₱{route.baseFare.toFixed(2)} base fare</span>
        </div>
      </div>

      {/* Map */}
      <RouteMap waypoints={route.waypoints} editable={false} height="350px" />

      {/* Waypoint list */}
      <div className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-gray-800 dark:text-white">
          Stops ({route.waypoints.length})
        </h2>
        <ol className="flex flex-col gap-1" aria-label="Route stops">
          {route.waypoints
            .sort((a, b) => a.position - b.position)
            .map((wp, index) => (
              <li
                key={`detail-wp-${index}`}
                className="flex items-center gap-2 rounded-md bg-gray-50 dark:bg-white/5 px-3 py-2 text-sm"
              >
                <span className="flex h-6 w-6 items-center justify-center rounded-full bg-blue-600 text-xs font-bold text-white">
                  {index + 1}
                </span>
                <span className="text-gray-800 dark:text-white">{wp.name || `Stop ${index + 1}`}</span>
                <span className="ml-auto text-xs text-gray-400 dark:text-white/40">
                  {wp.lat.toFixed(4)}, {wp.lng.toFixed(4)}
                </span>
              </li>
            ))}
        </ol>
      </div>

      {/* Vote panel */}
      <VoteRoutePanel routeId={routeId} voteCounts={route.voteCounts} />

      {/* Edit button */}
      {onEditRoute && (
        <button
          type="button"
          onClick={() => onEditRoute(routeId)}
          className="min-h-[44px] rounded-lg border border-blue-600 px-4 py-2 text-base font-medium text-blue-600 dark:text-blue-400 transition-colors hover:bg-blue-50 dark:hover:bg-blue-500/10 focus:outline-none focus:ring-2 focus:ring-blue-200 dark:focus:ring-blue-500/30"
          aria-label="Edit this route"
        >
          Suggest Edit (Submit Revision)
        </button>
      )}
    </div>
  );
}
