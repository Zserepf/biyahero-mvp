'use client';

/**
 * CreateRouteForm — Form for plotting a new route with ≥2 waypoints.
 *
 * Includes a map for waypoint plotting, vehicle type selection, and route name/fare inputs.
 * Validates with Zod before submission. Offline writes queued via task 12.5.
 *
 * Requirements: 1.1, 1.7, 1.8, 6.4, 9.1
 */

import { useState, useCallback } from 'react';
import { createRouteSchema, PH_LAT_MIN, PH_LAT_MAX, PH_LNG_MIN, PH_LNG_MAX } from '../schema';
import { useCreateRoute } from './useCreateRoute';
import { RouteMap } from '../components/RouteMap';
import type { Waypoint, VehicleType, CreateRouteRequest } from '../types';

interface CreateRouteFormProps {
  onSuccess?: () => void;
}

export function CreateRouteForm({ onSuccess }: CreateRouteFormProps) {
  const [name, setName] = useState('');
  const [vehicleType, setVehicleType] = useState<VehicleType>('jeepney');
  const [baseFare, setBaseFare] = useState<number>(13);
  const [waypoints, setWaypoints] = useState<Waypoint[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});

  const createRoute = useCreateRoute();

  const handleWaypointAdd = useCallback((lat: number, lng: number) => {
    // Validate Philippines bbox
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
    // Clear waypoint errors when adding
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
      name,
      vehicleType,
      baseFare,
      waypoints: waypoints.map((wp) => ({
        lat: wp.lat,
        lng: wp.lng,
        position: wp.position,
        name: wp.name,
      })),
    };

    const result = createRouteSchema.safeParse(formData);

    if (!result.success) {
      const fieldErrors: Record<string, string> = {};
      for (const issue of result.error.issues) {
        const path = issue.path.join('.');
        fieldErrors[path] = issue.message;
      }
      setErrors(fieldErrors);
      return;
    }

    const request: CreateRouteRequest = result.data;

    try {
      await createRoute.mutateAsync(request);
      onSuccess?.();
    } catch {
      setErrors({ form: 'routes.submitFailed' });
    }
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      {/* Route Name */}
      <div className="flex flex-col gap-1">
        <label htmlFor="route-name" className="text-sm font-medium text-gray-700">
          Route Name
        </label>
        <input
          id="route-name"
          type="text"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g., Cubao to Antipolo via Marcos Highway"
          className="min-h-[44px] rounded-lg border border-gray-300 px-3 py-2 text-base focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
          aria-invalid={!!errors.name}
          aria-describedby={errors.name ? 'route-name-error' : undefined}
        />
        {errors.name && (
          <p id="route-name-error" className="text-sm text-red-600" role="alert">
            {errors.name}
          </p>
        )}
      </div>

      {/* Vehicle Type */}
      <div className="flex flex-col gap-1">
        <label htmlFor="vehicle-type" className="text-sm font-medium text-gray-700">
          Vehicle Type
        </label>
        <select
          id="vehicle-type"
          value={vehicleType}
          onChange={(e) => setVehicleType(e.target.value as VehicleType)}
          className="min-h-[44px] rounded-lg border border-gray-300 px-3 py-2 text-base focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
        >
          <option value="jeepney">Jeepney</option>
          <option value="uv_express">UV Express</option>
          <option value="bus">Bus</option>
        </select>
      </div>

      {/* Base Fare */}
      <div className="flex flex-col gap-1">
        <label htmlFor="base-fare" className="text-sm font-medium text-gray-700">
          Base Fare (PHP)
        </label>
        <input
          id="base-fare"
          type="number"
          min={0}
          step={0.25}
          value={baseFare}
          onChange={(e) => setBaseFare(parseFloat(e.target.value) || 0)}
          className="min-h-[44px] rounded-lg border border-gray-300 px-3 py-2 text-base focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
          aria-invalid={!!errors.baseFare}
          aria-describedby={errors.baseFare ? 'base-fare-error' : undefined}
        />
        {errors.baseFare && (
          <p id="base-fare-error" className="text-sm text-red-600" role="alert">
            {errors.baseFare}
          </p>
        )}
      </div>

      {/* Map for waypoint plotting */}
      <div className="flex flex-col gap-1">
        <p className="text-sm font-medium text-gray-700">
          Plot Waypoints (tap map to add, minimum 2)
        </p>
        <RouteMap
          waypoints={waypoints}
          editable={true}
          onWaypointAdd={handleWaypointAdd}
          height="350px"
        />
        {errors.waypoints && (
          <p className="text-sm text-red-600" role="alert">
            {errors.waypoints}
          </p>
        )}
      </div>

      {/* Waypoint list */}
      {waypoints.length > 0 && (
        <div className="flex flex-col gap-2">
          <p className="text-sm font-medium text-gray-700">
            Waypoints ({waypoints.length})
          </p>
          <ul className="flex flex-col gap-1" aria-label="Plotted waypoints">
            {waypoints.map((wp, index) => (
              <li
                key={`wp-item-${index}`}
                className="flex items-center justify-between rounded-md bg-gray-50 px-3 py-2 text-sm"
              >
                <span>
                  {wp.name || `Waypoint ${index + 1}`} — {wp.lat.toFixed(5)},{' '}
                  {wp.lng.toFixed(5)}
                </span>
                <button
                  type="button"
                  onClick={() => handleRemoveWaypoint(index)}
                  className="min-h-[44px] min-w-[44px] rounded-md text-red-600 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-200"
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
        <p className="text-sm text-red-600" role="alert">
          {errors.form}
        </p>
      )}

      {/* Submit button */}
      <button
        type="submit"
        disabled={createRoute.isPending || waypoints.length < 2}
        className="min-h-[44px] rounded-lg bg-blue-600 px-4 py-3 text-base font-semibold text-white transition-colors hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-300 disabled:cursor-not-allowed disabled:bg-gray-400"
        aria-label="Submit route"
      >
        {createRoute.isPending ? 'Submitting...' : 'Submit Route'}
      </button>
    </form>
  );
}
