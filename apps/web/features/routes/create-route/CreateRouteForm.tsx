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
    <form onSubmit={handleSubmit} className="flex flex-col gap-5">
      {/* Route Name */}
      <div className="flex flex-col gap-1.5">
        <label htmlFor="route-name" className="text-sm font-medium text-gray-700 dark:text-white/80">
          Route Name
        </label>
        <input
          id="route-name"
          type="text"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g., Cubao to Antipolo via Marcos Highway"
          className="min-h-[44px] rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 px-4 py-2.5 text-sm text-gray-900 dark:text-white placeholder:text-gray-400 dark:placeholder:text-white/30 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition"
          aria-invalid={!!errors.name}
          aria-describedby={errors.name ? 'route-name-error' : undefined}
        />
        {errors.name && (
          <p id="route-name-error" className="flex items-center gap-1 text-xs text-red-500 dark:text-red-400" role="alert">
            <svg className="h-3.5 w-3.5 shrink-0" fill="currentColor" viewBox="0 0 20 20"><path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" /></svg>
            {errors.name}
          </p>
        )}
      </div>

      {/* Vehicle Type + Base Fare side by side */}
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="vehicle-type" className="text-sm font-medium text-gray-700 dark:text-white/80">
            Vehicle Type
          </label>
          <select
            id="vehicle-type"
            value={vehicleType}
            onChange={(e) => setVehicleType(e.target.value as VehicleType)}
            className="min-h-[44px] rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-slate-800 px-4 py-2.5 text-sm text-gray-900 dark:text-white focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition"
          >
            <option value="jeepney">Jeepney</option>
            <option value="uv_express">UV Express</option>
            <option value="bus">Bus</option>
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="base-fare" className="text-sm font-medium text-gray-700 dark:text-white/80">
            Base Fare (PHP)
          </label>
          <input
            id="base-fare"
            type="number"
            min={0}
            step={0.25}
            value={baseFare}
            onChange={(e) => setBaseFare(parseFloat(e.target.value) || 0)}
            className="min-h-[44px] rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 px-4 py-2.5 text-sm text-gray-900 dark:text-white placeholder:text-gray-400 dark:placeholder:text-white/30 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition"
            aria-invalid={!!errors.baseFare}
            aria-describedby={errors.baseFare ? 'base-fare-error' : undefined}
          />
          {errors.baseFare && (
            <p id="base-fare-error" className="text-xs text-red-500 dark:text-red-400" role="alert">{errors.baseFare}</p>
          )}
        </div>
      </div>

      {/* Map */}
      <div className="flex flex-col gap-1.5">
        <div className="flex items-center justify-between">
          <p className="text-sm font-medium text-gray-700 dark:text-white/80">
            Plot Waypoints
          </p>
          <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${waypoints.length >= 2 ? 'bg-emerald-500/20 text-emerald-600 dark:text-emerald-400' : 'bg-gray-100 dark:bg-white/10 text-gray-400 dark:text-white/40'}`}>
            {waypoints.length} / 2 min
          </span>
        </div>
        <div className="overflow-hidden rounded-xl border border-gray-200 dark:border-white/10">
          <RouteMap
            waypoints={waypoints}
            editable={true}
            onWaypointAdd={handleWaypointAdd}
            height="320px"
          />
        </div>
        {errors.waypoints && (
          <p className="flex items-center gap-1 text-xs text-red-500 dark:text-red-400" role="alert">
            <svg className="h-3.5 w-3.5 shrink-0" fill="currentColor" viewBox="0 0 20 20"><path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" /></svg>
            {errors.waypoints}
          </p>
        )}
      </div>

      {/* Waypoint list */}
      {waypoints.length > 0 && (
        <div className="flex flex-col gap-2">
          <p className="text-sm font-medium text-gray-700 dark:text-white/80">Waypoints ({waypoints.length})</p>
          <ul className="flex flex-col gap-1.5" aria-label="Plotted waypoints">
            {waypoints.map((wp, index) => (
              <li
                key={`wp-item-${index}`}
                className="flex items-center justify-between rounded-xl border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5 px-4 py-2.5"
              >
                <div className="flex items-center gap-3">
                  <span className="flex h-6 w-6 items-center justify-center rounded-full bg-blue-600 text-[10px] font-bold text-white">
                    {index + 1}
                  </span>
                  <span className="text-sm text-gray-700 dark:text-white/70">
                    {wp.name || `Waypoint ${index + 1}`}
                    <span className="ml-2 text-xs text-gray-400 dark:text-white/30">{wp.lat.toFixed(4)}, {wp.lng.toFixed(4)}</span>
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => handleRemoveWaypoint(index)}
                  className="flex h-8 w-8 items-center justify-center rounded-lg text-gray-400 dark:text-white/40 hover:bg-red-500/20 hover:text-red-500 dark:hover:text-red-400 focus:outline-none focus:ring-2 focus:ring-red-400/30 transition"
                  aria-label={`Remove waypoint ${index + 1}`}
                >
                  <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Form-level error */}
      {errors.form && (
        <p className="flex items-center gap-1 text-sm text-red-500 dark:text-red-400" role="alert">
          <svg className="h-4 w-4 shrink-0" fill="currentColor" viewBox="0 0 20 20"><path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" /></svg>
          {errors.form}
        </p>
      )}

      {/* Submit */}
      <button
        type="submit"
        disabled={createRoute.isPending || waypoints.length < 2}
        className="min-h-[44px] w-full rounded-xl bg-blue-600 px-4 py-3 font-semibold text-white shadow-lg shadow-blue-500/20 transition hover:bg-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/50 disabled:cursor-not-allowed disabled:opacity-40"
        aria-label="Submit route"
      >
        {createRoute.isPending ? (
          <span className="flex items-center justify-center gap-2">
            <span className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
            Submitting…
          </span>
        ) : (
          'Submit Route'
        )}
      </button>
    </form>
  );
}
