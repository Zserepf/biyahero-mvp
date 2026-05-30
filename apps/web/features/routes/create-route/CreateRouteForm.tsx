'use client';

/**
 * CreateRouteForm — Journey planner with multi-segment support.
 * Each segment = one transport leg (Jeepney, Bus, UV Express, Tricycle, Walk).
 * Requirements: 1.1, 1.7, 1.8, 6.4, 9.1
 */

import { useState, useCallback, useRef } from 'react';
import dynamic from 'next/dynamic';
import { createRouteSchema, PH_LAT_MIN, PH_LAT_MAX, PH_LNG_MIN, PH_LNG_MAX } from '../schema';
import { useCreateRoute } from './useCreateRoute';
import { LocationSearch } from '../components/LocationSearch';
import type { Waypoint, VehicleType } from '../types';
import type { Map as LeafletMap } from 'leaflet';

const RouteMap = dynamic(
  () => import('../components/RouteMap').then((m) => m.RouteMap),
  { ssr: false, loading: () => <div className="h-[240px] rounded-xl border border-white/10 bg-white/5 animate-pulse" /> },
);

interface Segment {
  id: string;
  vehicleType: VehicleType;
  baseFare: number;
  waypoints: Waypoint[];
}

interface CreateRouteFormProps {
  onSuccess?: () => void;
}

const TRANSPORT_OPTIONS: { value: VehicleType; emoji: string; label: string; defaultFare: number }[] = [
  { value: 'jeepney',   emoji: '🚌', label: 'Jeepney',    defaultFare: 13 },
  { value: 'bus',       emoji: '🚍', label: 'Bus',         defaultFare: 15 },
  { value: 'uv_express',emoji: '🚐', label: 'UV Express',  defaultFare: 20 },
  { value: 'tricycle',  emoji: '🛺', label: 'Tricycle',    defaultFare: 10 },
  { value: 'walk',      emoji: '🚶', label: 'Walk',        defaultFare: 0  },
];

function newSegment(vehicleType: VehicleType = 'jeepney', defaultFare = 13): Segment {
  return { id: crypto.randomUUID(), vehicleType, baseFare: defaultFare, waypoints: [] };
}

export function CreateRouteForm({ onSuccess }: CreateRouteFormProps) {
  const [journeyName, setJourneyName] = useState('');
  const [segments, setSegments] = useState<Segment[]>([newSegment()]);
  const [activeSegmentIdx, setActiveSegmentIdx] = useState(0);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  // One map ref per segment — indexed by segment index
  const mapRefs = useRef<Record<number, LeafletMap | null>>({});
  const getMapRef = (idx: number): React.MutableRefObject<LeafletMap | null> => {
    if (!mapRefs.current[idx]) mapRefs.current[idx] = null;
    return {
      get current() { return mapRefs.current[idx] ?? null; },
      set current(v) { mapRefs.current[idx] = v; },
    };
  };

  const createRoute = useCreateRoute();
  const activeSegment = segments[activeSegmentIdx];

  const handleWaypointAdd = useCallback((lat: number, lng: number, name?: string) => {
    if (lat < PH_LAT_MIN || lat > PH_LAT_MAX || lng < PH_LNG_MIN || lng > PH_LNG_MAX) {
      setErrors((prev) => ({ ...prev, waypoints: 'Waypoint is outside the Philippines.' }));
      return;
    }
    setSegments((prev) =>
      prev.map((seg, i) =>
        i !== activeSegmentIdx ? seg : {
          ...seg,
          waypoints: seg.waypoints.length >= 2 ? seg.waypoints : [
            ...seg.waypoints,
            { lat, lng, position: seg.waypoints.length, name },
          ],
        },
      ),
    );
    setErrors((prev) => { const { waypoints: _, ...rest } = prev; return rest; });
  }, [activeSegmentIdx]);

  // Called by LocationSearch — pan the map then add the waypoint
  const handleLocationSelect = useCallback((segIdx: number, lat: number, lng: number, name: string) => {
    // Pan the map for this segment
    const mapInstance = mapRefs.current[segIdx];
    if (mapInstance) {
      mapInstance.setView([lat, lng], 15, { animate: true });
    }
    // Add as waypoint
    if (lat < PH_LAT_MIN || lat > PH_LAT_MAX || lng < PH_LNG_MIN || lng > PH_LNG_MAX) {
      setErrors((prev) => ({ ...prev, waypoints: 'Waypoint is outside the Philippines.' }));
      return;
    }
    setSegments((prev) =>
      prev.map((seg, i) =>
        i !== segIdx ? seg : {
          ...seg,
          waypoints: seg.waypoints.length >= 2 ? seg.waypoints : [
            ...seg.waypoints,
            { lat, lng, position: seg.waypoints.length, name },
          ],
        },
      ),
    );
    setErrors((prev) => { const { waypoints: _, ...rest } = prev; return rest; });
  }, []);

  const handleRemoveWaypoint = useCallback((segIdx: number, wpIdx: number) => {
    setSegments((prev) =>
      prev.map((seg, i) =>
        i !== segIdx ? seg : {
          ...seg,
          waypoints: seg.waypoints
            .filter((_, j) => j !== wpIdx)
            .map((wp, j) => ({ ...wp, position: j })),
        },
      ),
    );
  }, []);

  const handleVehicleChange = (segIdx: number, v: VehicleType) => {
    const opt = TRANSPORT_OPTIONS.find((o) => o.value === v);
    setSegments((prev) =>
      prev.map((seg, i) =>
        i !== segIdx ? seg : { ...seg, vehicleType: v, baseFare: opt?.defaultFare ?? 0 },
      ),
    );
  };

  const addSegment = (vehicleType: VehicleType) => {
    const opt = TRANSPORT_OPTIONS.find((o) => o.value === vehicleType);
    const prev = segments[segments.length - 1];
    const lastWp = prev?.waypoints[prev.waypoints.length - 1];
    const next = newSegment(vehicleType, opt?.defaultFare ?? 0);
    if (lastWp) next.waypoints = [{ ...lastWp, position: 0 }];
    setSegments((s) => [...s, next]);
    setActiveSegmentIdx(segments.length);
  };

  const removeSegment = (idx: number) => {
    if (segments.length === 1) return;
    setSegments((s) => s.filter((_, i) => i !== idx));
    setActiveSegmentIdx((prev) => Math.min(prev, segments.length - 2));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrors({});

    if (!journeyName.trim()) {
      setErrors({ journeyName: 'Journey name is required.' });
      return;
    }

    for (let i = 0; i < segments.length; i++) {
      if (segments[i].waypoints.length < 2) {
        setErrors({ [`seg_${i}`]: `Leg ${i + 1} needs 2 waypoints on the map.` });
        setActiveSegmentIdx(i);
        return;
      }
    }

    setIsSubmitting(true);
    try {
      for (let i = 0; i < segments.length; i++) {
        const seg = segments[i];
        const opt = TRANSPORT_OPTIONS.find((o) => o.value === seg.vehicleType);
        const segName = segments.length === 1
          ? journeyName
          : `${journeyName} — Leg ${i + 1} (${opt?.emoji} ${opt?.label})`;

        const request = {
          name: segName,
          vehicleType: seg.vehicleType === 'walk' ? 'jeepney' : seg.vehicleType,
          baseFare: seg.baseFare,
          waypoints: seg.waypoints.map((wp) => ({
            lat: wp.lat, lng: wp.lng, position: wp.position, name: wp.name,
          })),
        };

        const parsed = createRouteSchema.safeParse(request);
        if (!parsed.success) {
          setErrors({ [`seg_${i}`]: parsed.error.issues[0]?.message ?? 'Invalid segment.' });
          setActiveSegmentIdx(i);
          setIsSubmitting(false);
          return;
        }

        await createRoute.mutateAsync(parsed.data);
      }
      onSuccess?.();
    } catch {
      setErrors({ form: 'Submit failed. Make sure you are logged in and try again.' });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-6">

      {/* Journey name */}
      <div className="flex flex-col gap-1.5">
        <label htmlFor="journey-name" className="text-sm font-medium text-white/80">
          Journey Name
        </label>
        <input
          id="journey-name"
          type="text"
          value={journeyName}
          onChange={(e) => setJourneyName(e.target.value)}
          placeholder="e.g., Taytay to Antipolo"
          className="min-h-[44px] rounded-xl border border-white/10 bg-white/5 px-4 py-2.5 text-sm text-white placeholder:text-white/30 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition"
        />
        {errors.journeyName && <p className="text-xs text-red-400" role="alert">{errors.journeyName}</p>}
      </div>

      {/* Journey legs — vertical timeline */}
      <div className="flex flex-col gap-3">
        <p className="text-sm font-semibold text-white/80">Journey Legs</p>

        {segments.map((seg, i) => {
          const opt = TRANSPORT_OPTIONS.find((o) => o.value === seg.vehicleType);
          const isActive = i === activeSegmentIdx;
          return (
            <div key={seg.id} className="flex gap-3">
              {/* Timeline connector */}
              <div className="flex flex-col items-center">
                <button
                  type="button"
                  onClick={() => setActiveSegmentIdx(i)}
                  className={`flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-full text-xl transition ${
                    isActive ? 'bg-blue-600 ring-2 ring-blue-400/50' : 'bg-white/10 hover:bg-white/20'
                  }`}
                  aria-label={`Edit leg ${i + 1}`}
                >
                  {opt?.emoji}
                </button>
                {i < segments.length - 1 && (
                  <div className="mt-1 h-full w-0.5 bg-white/10" />
                )}
              </div>

              {/* Leg card */}
              <div className={`flex-1 rounded-2xl border p-4 transition ${
                isActive ? 'border-blue-500/40 bg-blue-500/10' : 'border-white/10 bg-white/5'
              }`}>
                <div className="flex items-center justify-between mb-3">
                  <p className="text-sm font-semibold text-white">
                    Leg {i + 1} · {opt?.emoji} {opt?.label}
                  </p>
                  {segments.length > 1 && (
                    <button
                      type="button"
                      onClick={() => removeSegment(i)}
                      className="text-xs text-white/30 hover:text-red-400 transition"
                      aria-label={`Remove leg ${i + 1}`}
                    >
                      Remove
                    </button>
                  )}
                </div>

                {isActive ? (
                  <div className="flex flex-col gap-3">
                    {/* Vehicle selector — big tappable cards */}
                    <div>
                      <p className="mb-2 text-xs font-medium text-white/50">How are you getting there?</p>
                      <div className="grid grid-cols-5 gap-2">
                        {TRANSPORT_OPTIONS.map((o) => (
                          <button
                            key={o.value}
                            type="button"
                            onClick={() => handleVehicleChange(i, o.value)}
                            className={`flex flex-col items-center gap-1 rounded-xl py-3 text-center transition ${
                              seg.vehicleType === o.value
                                ? 'bg-blue-600 text-white'
                                : 'bg-white/10 text-white/60 hover:bg-white/20'
                            }`}
                          >
                            <span className="text-xl">{o.emoji}</span>
                            <span className="text-[10px] font-medium leading-tight">{o.label}</span>
                          </button>
                        ))}
                      </div>
                    </div>

                    {/* Fare */}
                    {seg.vehicleType !== 'walk' && (
                      <div className="flex flex-col gap-1">
                        <label className="text-xs font-medium text-white/50">Base Fare (PHP)</label>
                        <input
                          type="number"
                          min={0}
                          step={0.25}
                          value={seg.baseFare}
                          onChange={(e) =>
                            setSegments((prev) =>
                              prev.map((s, j) =>
                                j !== i ? s : { ...s, baseFare: parseFloat(e.target.value) || 0 },
                              ),
                            )
                          }
                          className="min-h-[44px] rounded-xl border border-white/10 bg-white/5 px-3 py-2 text-sm text-white focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition"
                        />
                      </div>
                    )}

                    {/* Map */}
                    <div className="flex flex-col gap-1.5">
                      <div className="flex items-center justify-between">
                        <p className="text-xs font-medium text-white/50">
                          Tap map to set start &amp; end points
                        </p>
                        <span className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${
                          seg.waypoints.length >= 2
                            ? 'bg-emerald-500/20 text-emerald-400'
                            : 'bg-white/10 text-white/40'
                        }`}>
                          {seg.waypoints.length} / 2
                        </span>
                      </div>
                      {/* Location search — above the map, outside MapContainer */}
                      {seg.waypoints.length < 2 && (
                        <div className="relative" style={{ zIndex: 1000 }}>
                          <LocationSearch
                            placeholder="Search a place to add as waypoint…"
                            onLocationSelect={(lat, lng, name) => handleLocationSelect(i, lat, lng, name)}
                          />
                        </div>
                      )}
                      <div className="rounded-xl border border-white/10" style={{ position: 'relative', zIndex: 1 }}>
                        <RouteMap
                          waypoints={seg.waypoints}
                          editable={true}
                          onWaypointAdd={handleWaypointAdd}
                          height="240px"
                          mapRef={getMapRef(i)}
                        />
                      </div>
                      {errors.waypoints && <p className="text-xs text-red-400" role="alert">{errors.waypoints}</p>}
                      {errors[`seg_${i}`] && <p className="text-xs text-red-400" role="alert">{errors[`seg_${i}`]}</p>}
                    </div>

                    {/* Waypoint list */}
                    {seg.waypoints.length > 0 && (
                      <ul className="flex flex-col gap-1.5">
                        {seg.waypoints.map((wp, idx) => (
                          <li key={idx} className="flex items-center justify-between rounded-xl border border-white/10 bg-white/5 px-3 py-2">
                            <div className="flex items-center gap-2">
                              <span className="flex h-5 w-5 items-center justify-center rounded-full bg-blue-600 text-[10px] font-bold text-white">{idx + 1}</span>
                              <div className="flex flex-col">
                                {wp.name && <span className="text-xs font-medium text-white/80 leading-tight">{wp.name}</span>}
                                <span className="text-xs text-white/40">{wp.lat.toFixed(4)}, {wp.lng.toFixed(4)}</span>
                              </div>
                            </div>
                            <button
                              type="button"
                              onClick={() => handleRemoveWaypoint(i, idx)}
                              className="text-white/30 hover:text-red-400 transition"
                              aria-label={`Remove waypoint ${idx + 1}`}
                            >
                              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                                <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                              </svg>
                            </button>
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                ) : (
                  /* Collapsed summary */
                  <p className="text-xs text-white/40">
                    {seg.waypoints.length >= 2
                      ? `${seg.waypoints[0].name || `${seg.waypoints[0].lat.toFixed(4)}, ${seg.waypoints[0].lng.toFixed(4)}`} → ${seg.waypoints[1].name || `${seg.waypoints[1].lat.toFixed(4)}, ${seg.waypoints[1].lng.toFixed(4)}`}`
                      : 'Tap to set waypoints'}
                  </p>
                )}
              </div>
            </div>
          );
        })}

        {/* Add next leg — big visible section */}
        <div className="flex gap-3">
          <div className="flex w-10 flex-shrink-0 items-start justify-center pt-1">
            <div className="h-4 w-0.5 bg-white/10" />
          </div>
          <div className="flex-1">
            <p className="mb-2 text-sm font-semibold text-white/60">Add another leg?</p>
            <div className="grid grid-cols-5 gap-2">
              {TRANSPORT_OPTIONS.map((o) => (
                <button
                  key={o.value}
                  type="button"
                  onClick={() => addSegment(o.value)}
                  className="flex flex-col items-center gap-1 rounded-xl border border-dashed border-white/20 py-3 text-center text-white/50 transition hover:border-white/40 hover:bg-white/10 hover:text-white/80"
                >
                  <span className="text-xl">{o.emoji}</span>
                  <span className="text-[10px] font-medium leading-tight">{o.label}</span>
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Form error */}
      {errors.form && (
        <div className="flex items-center gap-2 rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-3" role="alert">
          <svg className="h-4 w-4 shrink-0 text-red-400" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
          </svg>
          <p className="text-sm text-red-400">{errors.form}</p>
        </div>
      )}

      {/* Submit */}
      <button
        type="submit"
        disabled={isSubmitting}
        className="min-h-[48px] w-full rounded-xl bg-blue-600 px-4 py-3 font-semibold text-white shadow-lg shadow-blue-500/20 transition hover:bg-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/50 disabled:cursor-not-allowed disabled:opacity-40"
      >
        {isSubmitting ? (
          <span className="flex items-center justify-center gap-2">
            <span className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
            Submitting…
          </span>
        ) : (
          segments.length > 1 ? `Submit ${segments.length} Legs` : 'Submit Route'
        )}
      </button>
    </form>
  );
}
