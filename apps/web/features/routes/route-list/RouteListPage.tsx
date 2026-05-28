'use client';

/**
 * RouteListPage — Page component for browsing routes on a map.
 *
 * Displays a map with routes loaded via bbox query. Routes are fetched
 * as the user pans/zooms the map.
 *
 * Requirements: 1.2
 */

import { useState, useCallback } from 'react';
import { RouteMap } from '../components/RouteMap';
import { useRouteList } from './useRouteList';
import type { BboxQuery, RouteDto } from '../types';

interface RouteListPageProps {
  onRouteSelect?: (route: RouteDto) => void;
}

export function RouteListPage({ onRouteSelect }: RouteListPageProps) {
  const [bbox, setBbox] = useState<BboxQuery | null>(null);
  const { data: routes, isLoading } = useRouteList(bbox);

  const handleBoundsChange = useCallback(
    (bounds: { swLat: number; swLng: number; neLat: number; neLng: number }) => {
      setBbox({
        bboxSwLat: bounds.swLat,
        bboxSwLng: bounds.swLng,
        bboxNeLat: bounds.neLat,
        bboxNeLng: bounds.neLng,
      });
    },
    [],
  );

  // Flatten all waypoints from all routes for display
  const allWaypoints =
    routes?.flatMap((route) =>
      route.waypoints.map((wp) => ({
        ...wp,
        name: `${route.name} — ${wp.name || `Stop ${wp.position + 1}`}`,
      })),
    ) ?? [];

  return (
    <div className="flex flex-col gap-4 p-4">
      <h1 className="text-xl font-bold text-gray-900">Browse Routes</h1>
      <p className="text-sm text-gray-600">
        Pan and zoom the map to discover community-submitted routes in your area.
      </p>

      {/* Map with bbox-based route loading */}
      <RouteMap
        waypoints={allWaypoints}
        editable={false}
        onBoundsChange={handleBoundsChange}
        height="400px"
      />

      {/* Loading indicator */}
      {isLoading && (
        <p className="text-sm text-gray-500" aria-live="polite">
          Loading routes...
        </p>
      )}

      {/* Route list below map */}
      {routes && routes.length > 0 && (
        <div className="flex flex-col gap-2">
          <h2 className="text-base font-semibold text-gray-800">
            Routes in View ({routes.length})
          </h2>
          <ul className="flex flex-col gap-2" aria-label="Routes in current map view">
            {routes.map((route) => (
              <li key={route.id}>
                <button
                  type="button"
                  onClick={() => onRouteSelect?.(route)}
                  className="flex w-full min-h-[44px] items-center justify-between rounded-lg border border-gray-200 bg-white px-4 py-3 text-left transition-colors hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-200"
                  aria-label={`View route: ${route.name}`}
                >
                  <div className="flex flex-col">
                    <span className="text-sm font-medium text-gray-900">
                      {route.name}
                    </span>
                    <span className="text-xs text-gray-500">
                      {route.vehicleType} • {route.waypoints.length} stops •{' '}
                      {route.status}
                    </span>
                  </div>
                  <div className="flex items-center gap-2 text-xs text-gray-500">
                    <span className="text-green-600">
                      ✓ {route.voteCounts.stillAccurate}
                    </span>
                    <span className="text-red-600">
                      ✗ {route.voteCounts.noLongerAccurate}
                    </span>
                  </div>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {routes && routes.length === 0 && !isLoading && (
        <p className="text-sm text-gray-500 text-center py-4">
          No routes found in this area. Be the first to plot one!
        </p>
      )}
    </div>
  );
}
