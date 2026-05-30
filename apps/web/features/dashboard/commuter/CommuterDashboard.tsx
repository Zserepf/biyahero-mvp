'use client';

/**
 * CommuterDashboard — Mobile-first PWA home screen for commuters.
 *
 * Layout:
 *  - Fixed full-height shell (no page scroll)
 *  - Top bar: fixed, never scrolls
 *  - Content area: scrollable
 *  - Bottom nav: fixed, always visible — Home · Routes · Fare · Profile
 *  - Fare tab: full calculator inline (no navigation away)
 *  - Routes tab: browse list + create form inline (no navigation away)
 */

import { useState, useCallback, useMemo, type FormEvent } from 'react';
import dynamic from 'next/dynamic';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMe } from '@/features/auth/useMe';
import { useLogout } from '@/features/auth/useLogout';
import { fareCalculateSchema, VEHICLE_TYPES, DISCOUNT_CATEGORIES } from '@/features/fare/calculate-fare/schema';
import { useCalculateFare } from '@/features/fare/calculate-fare/useCalculateFare';
import { FareResult } from '@/features/fare/calculate-fare/FareResult';
import { useRouteList } from '@/features/routes/route-list/useRouteList';
import { CreateRouteForm } from '@/features/routes/create-route/CreateRouteForm';
import type { VehicleType, DiscountCategory, Coordinate } from '@/features/fare/calculate-fare/types';

type Tab = 'home' | 'routes' | 'fare' | 'profile';
type RoutesView = 'menu' | 'browse' | 'create';

const FareMapPicker = dynamic(
  () => import('@/features/fare/calculate-fare/FareMapPicker').then((m) => m.FareMapPicker),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-[220px] w-full items-center justify-center rounded-xl border border-white/10 bg-white/5">
        <span className="h-5 w-5 animate-spin rounded-full border-2 border-emerald-500 border-t-transparent" />
      </div>
    ),
  },
);

const RouteMap = dynamic(
  () => import('@/features/routes/components/RouteMap').then((m) => m.RouteMap),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-[200px] w-full items-center justify-center rounded-xl border border-white/10 bg-white/5">
        <span className="h-5 w-5 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
      </div>
    ),
  },
);

const VEHICLE_TYPE_LABELS: Record<VehicleType, string> = {
  Jeepney: 'Jeepney', Bus: 'Bus', UV_Express: 'UV Express', Tricycle: 'Tricycle',
};
const DISCOUNT_CATEGORY_LABELS: Record<DiscountCategory, string> = {
  regular: 'Regular', student: 'Student (20% off)', senior: 'Senior Citizen (20% off)', pwd: 'PWD (20% off)',
};
const VEHICLE_ICONS: Record<string, string> = {
  Jeepney: '🚌', Bus: '🚍', UV_Express: '🚐', Tricycle: '🛺',
};

// ─── Home tab ─────────────────────────────────────────────────────────────────

function HomeTab({ onGoFare, onGoRoutes }: { onGoFare: () => void; onGoRoutes: () => void }) {
  return (
    <div className="flex flex-col gap-4">
      <Link
        href="/commuter/waiting"
        className="flex min-h-[80px] w-full items-center gap-4 rounded-2xl bg-gradient-to-r from-amber-500 to-orange-500 px-5 py-4 shadow-xl shadow-amber-500/20 transition active:scale-[0.98]"
        aria-label="Signal that you are waiting for a ride"
      >
        <div className="flex h-12 w-12 flex-shrink-0 items-center justify-center rounded-xl bg-white/20">
          <svg className="h-6 w-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
            <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
        </div>
        <div className="flex-1">
          <p className="text-base font-bold text-white">I&apos;m Waiting Here</p>
          <p className="text-sm text-white/70">Signal your stop to nearby drivers</p>
        </div>
        <svg className="h-5 w-5 text-white/60" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
        </svg>
      </Link>

      <div className="grid grid-cols-2 gap-3">
        <button
          onClick={onGoFare}
          className="flex flex-col gap-2 rounded-2xl border border-emerald-500/20 bg-emerald-500/15 p-4 transition active:scale-95 text-left"
        >
          <span className="text-2xl">💰</span>
          <div>
            <p className="text-sm font-bold text-emerald-400">Fare Calculator</p>
            <p className="text-xs text-white/40 mt-0.5">LTFRB-compliant rates</p>
          </div>
        </button>
        <button
          onClick={onGoRoutes}
          className="flex flex-col gap-2 rounded-2xl border border-violet-500/20 bg-violet-500/15 p-4 transition active:scale-95 text-left"
        >
          <span className="text-2xl">✏️</span>
          <div>
            <p className="text-sm font-bold text-violet-400">Routes</p>
            <p className="text-xs text-white/40 mt-0.5">Browse &amp; contribute</p>
          </div>
        </button>
      </div>

      <div className="rounded-2xl border border-blue-500/20 bg-blue-500/10 px-4 py-3">
        <p className="text-xs font-semibold uppercase tracking-widest text-blue-400 mb-1">Tip</p>
        <p className="text-sm text-white/70">
          💡 Tap &quot;I&apos;m Waiting Here&quot; to signal your stop — nearby drivers will see your location on their heatmap.
        </p>
      </div>
    </div>
  );
}

// ─── Fare tab — full inline calculator ───────────────────────────────────────

function FareTab() {
  const [origin, setOrigin] = useState<Coordinate | null>(null);
  const [destination, setDestination] = useState<Coordinate | null>(null);
  const [vehicleType, setVehicleType] = useState<VehicleType | ''>('');
  const [discountCategory, setDiscountCategory] = useState<DiscountCategory>('regular');
  const [formError, setFormError] = useState<string | null>(null);
  const { calculateFare, result, isLoading, error } = useCalculateFare();

  const pinStep = !origin ? 'origin' : !destination ? 'destination' : 'done';

  const handleReset = () => { setOrigin(null); setDestination(null); setFormError(null); };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setFormError(null);
    if (!origin) { setFormError('Tap the map to set your origin point.'); return; }
    if (!destination) { setFormError('Tap the map again to set your destination.'); return; }
    if (!vehicleType) { setFormError('Select a vehicle type.'); return; }
    const parsed = fareCalculateSchema.safeParse({ origin, destination, vehicleType, discountCategory });
    if (!parsed.success) { setFormError(parsed.error.issues[0]?.message || 'Invalid input.'); return; }
    try { await calculateFare(parsed.data); } catch { /* captured in hook */ }
  };

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h2 className="text-base font-bold text-white">Fare Calculator</h2>
        <p className="mt-0.5 text-xs text-white/50">LTFRB-compliant rates with discounts</p>
      </div>

      {/* Step indicator */}
      <div className="flex items-center gap-2 flex-wrap">
        {[
          { key: 'origin', label: 'Origin', done: !!origin },
          { key: 'destination', label: 'Destination', done: !!destination },
          { key: 'done', label: 'Calculate', done: false },
        ].map((step, i) => (
          <div key={step.key} className="flex items-center gap-1.5">
            {i > 0 && <div className="h-px w-4 bg-white/10" />}
            <div className={`flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-medium ${
              step.done ? 'bg-emerald-500/20 text-emerald-400 ring-1 ring-emerald-500/30'
              : pinStep === step.key ? 'bg-blue-500/20 text-blue-300 ring-1 ring-blue-500/30'
              : 'bg-white/5 text-white/30'
            }`}>
              {step.done && <svg className="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}><path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" /></svg>}
              {step.label}
            </div>
          </div>
        ))}
      </div>

      {/* Map */}
      <div className="rounded-2xl border border-white/10 bg-white/5 p-3">
        <p className="mb-2 text-xs text-blue-300">
          {!origin ? 'Tap the map to set origin' : !destination ? 'Tap again to set destination' : 'Both pins set'}
        </p>
        <div className="overflow-hidden rounded-xl border border-white/10">
          <FareMapPicker origin={origin} destination={destination} onOriginChange={setOrigin} onDestinationChange={setDestination} />
        </div>
        <div className="mt-2 flex items-center justify-between flex-wrap gap-2">
          <div className="flex flex-wrap gap-1.5">
            {origin ? (
              <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/20 px-2 py-0.5 text-xs text-emerald-400 ring-1 ring-emerald-500/30">
                <span className="h-1.5 w-1.5 rounded-full bg-emerald-400" />{origin.lat.toFixed(4)}, {origin.lng.toFixed(4)}
              </span>
            ) : <span className="text-xs text-white/30 italic">No origin</span>}
            {destination && (
              <span className="inline-flex items-center gap-1 rounded-full bg-red-500/20 px-2 py-0.5 text-xs text-red-400 ring-1 ring-red-500/30">
                <span className="h-1.5 w-1.5 rounded-full bg-red-400" />{destination.lat.toFixed(4)}, {destination.lng.toFixed(4)}
              </span>
            )}
          </div>
          {(origin || destination) && (
            <button type="button" onClick={handleReset} className="rounded-lg px-2 py-1 text-xs text-white/40 hover:text-white/70 hover:bg-white/10 transition">Reset</button>
          )}
        </div>
      </div>

      {/* Form */}
      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-3">
        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-1">
            <label htmlFor="fare-vehicle" className="text-xs font-medium text-white/70">Vehicle Type</label>
            <select id="fare-vehicle" value={vehicleType} onChange={(e) => setVehicleType(e.target.value as VehicleType)}
              className="min-h-[44px] w-full rounded-xl border border-white/10 bg-slate-800 px-3 py-2 text-sm text-white focus:border-emerald-500 focus:outline-none focus:ring-2 focus:ring-emerald-500/30 transition">
              <option value="">Select type</option>
              {VEHICLE_TYPES.map((t) => <option key={t} value={t}>{VEHICLE_TYPE_LABELS[t]}</option>)}
            </select>
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="fare-discount" className="text-xs font-medium text-white/70">Discount</label>
            <select id="fare-discount" value={discountCategory} onChange={(e) => setDiscountCategory(e.target.value as DiscountCategory)}
              className="min-h-[44px] w-full rounded-xl border border-white/10 bg-slate-800 px-3 py-2 text-sm text-white focus:border-emerald-500 focus:outline-none focus:ring-2 focus:ring-emerald-500/30 transition">
              {DISCOUNT_CATEGORIES.map((c) => <option key={c} value={c}>{DISCOUNT_CATEGORY_LABELS[c]}</option>)}
            </select>
          </div>
        </div>

        {(formError || error) && (
          <div className="flex items-center gap-2 rounded-xl border border-red-500/20 bg-red-500/10 px-3 py-2" role="alert">
            <svg className="h-4 w-4 shrink-0 text-red-400" fill="currentColor" viewBox="0 0 20 20"><path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" /></svg>
            <p className="text-xs text-red-400">{formError || error}</p>
          </div>
        )}

        <button type="submit" disabled={isLoading}
          className="min-h-[44px] w-full rounded-xl bg-emerald-600 px-4 py-3 font-semibold text-white shadow-lg shadow-emerald-500/20 transition hover:bg-emerald-500 disabled:opacity-40 disabled:cursor-not-allowed">
          {isLoading ? <span className="flex items-center justify-center gap-2"><span className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />Calculating…</span> : 'Calculate Fare'}
        </button>
      </form>

      {result && (
        <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
          <FareResult result={result} />
        </div>
      )}
    </div>
  );
}

// ─── Routes tab — inline browse + create ─────────────────────────────────────

// Journey grouping helpers (mirrors RouteListPage logic)
function extractJourneyName(routeName: string): string {
  return routeName.replace(/\s*—\s*Leg\s+\d+.*$/i, '').trim();
}

interface Journey {
  name: string;
  legs: import('@/features/routes/types').RouteDto[];
  status: string;
}

function groupIntoJourneys(routes: import('@/features/routes/types').RouteDto[]): Journey[] {
  const map = new Map<string, import('@/features/routes/types').RouteDto[]>();
  for (const route of routes) {
    const key = extractJourneyName(route.name);
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(route);
  }
  return Array.from(map.entries()).map(([name, legs]) => ({
    name,
    legs: legs.sort((a, b) => {
      const na = parseInt(a.name.match(/Leg\s+(\d+)/i)?.[1] ?? '0');
      const nb = parseInt(b.name.match(/Leg\s+(\d+)/i)?.[1] ?? '0');
      return na - nb;
    }),
    status: legs.some((l) => l.status === 'verified') ? 'verified' : 'unverified',
  }));
}

const ROUTE_VEHICLE_ICONS: Record<string, string> = {
  jeepney: '🚌', bus: '🚍', uv_express: '🚐', tricycle: '🛺', walk: '🚶',
};
const ROUTE_VEHICLE_LABELS: Record<string, string> = {
  jeepney: 'Jeepney', bus: 'Bus', uv_express: 'UV Express', tricycle: 'Tricycle', walk: 'Walk',
};

function RoutesTab() {
  const [view, setView] = useState<RoutesView>('menu');
  const [search, setSearch] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [expandedJourney, setExpandedJourney] = useState<string | null>(null);

  // Always fetch all PH routes — no bbox dependency
  const { data: routes, isLoading } = useRouteList(null);

  const journeys = useMemo(() => {
    const all = routes ?? [];
    const filtered = search
      ? all.filter((r) => extractJourneyName(r.name).toLowerCase().includes(search.toLowerCase()))
      : all;
    return groupIntoJourneys(filtered);
  }, [routes, search]);

  // Collect all waypoints from the expanded journey for the map
  const mapWaypoints = useMemo(() => {
    if (!expandedJourney) return [];
    const journey = journeys.find((j) => j.name === expandedJourney);
    if (!journey) return [];
    let offset = 0;
    return journey.legs.flatMap((leg) => {
      const wps = leg.waypoints.map((wp) => ({ ...wp, position: wp.position + offset }));
      offset += leg.waypoints.length;
      return wps;
    });
  }, [expandedJourney, journeys]);

  const toggleJourney = (name: string) => {
    setExpandedJourney((prev) => (prev === name ? null : name));
  };

  if (view === 'create') {
    if (submitted) {
      return (
        <div className="flex flex-col items-center gap-4 py-8 text-center">
          <div className="flex h-16 w-16 items-center justify-center rounded-full bg-emerald-500/20 ring-1 ring-emerald-500/30">
            <svg className="h-8 w-8 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" /></svg>
          </div>
          <div>
            <p className="text-base font-bold text-white">Route Submitted!</p>
            <p className="mt-1 text-xs text-white/50">Route saved and live on the map.</p>
          </div>
          <div className="flex w-full flex-col gap-2">
            <button onClick={() => { setSubmitted(false); }} className="min-h-[44px] w-full rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white transition hover:bg-blue-500">Plot Another</button>
            <button onClick={() => setView('menu')} className="min-h-[44px] w-full rounded-xl border border-white/10 bg-white/5 px-4 py-3 text-sm font-semibold text-white transition hover:bg-white/10">Back to Routes</button>
          </div>
        </div>
      );
    }
    return (
      <div className="flex flex-col gap-4">
        <div className="flex items-center gap-3">
          <button onClick={() => setView('menu')} className="flex h-8 w-8 items-center justify-center rounded-xl bg-white/10 text-white transition hover:bg-white/20" aria-label="Back">
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}><path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" /></svg>
          </button>
          <div>
            <h2 className="text-base font-bold text-white">Plot a Route</h2>
            <p className="text-xs text-white/50">Contribute to the community transit map</p>
          </div>
        </div>
        <div className="rounded-2xl border border-blue-500/20 bg-blue-500/10 px-3 py-2">
          <p className="text-xs text-blue-300">Tap the map to add at least <strong>2 waypoints</strong>. Your route goes live immediately.</p>
        </div>
        <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
          <CreateRouteForm onSuccess={() => setSubmitted(true)} />
        </div>
      </div>
    );
  }

  if (view === 'browse') {
    return (
      <div className="flex flex-col gap-4">
        {/* Header */}
        <div className="flex items-center gap-3">
          <button onClick={() => { setView('menu'); setExpandedJourney(null); }} className="flex h-8 w-8 items-center justify-center rounded-xl bg-white/10 text-white transition hover:bg-white/20" aria-label="Back">
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}><path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" /></svg>
          </button>
          <div>
            <h2 className="text-base font-bold text-white">Browse Routes</h2>
            <p className="text-xs text-white/50">Community transit map</p>
          </div>
        </div>

        {/* Search */}
        <input type="text" placeholder="Search routes…" value={search} onChange={(e) => setSearch(e.target.value)}
          className="min-h-[44px] w-full rounded-xl border border-white/10 bg-white/5 px-4 py-2.5 text-sm text-white placeholder:text-white/30 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition" />

        {/* Map — clean slate, shows selected journey on tap */}
        <div className="overflow-hidden rounded-xl border border-white/10">
          <RouteMap
            waypoints={mapWaypoints}
            editable={false}
            height="200px"
            scrollWheelZoom={false}
          />
          <p className="border-t border-white/5 bg-white/5 px-3 py-1 text-center text-[10px] text-white/30">
            {expandedJourney ? `Showing: ${expandedJourney}` : 'Tap a journey to show it on the map'}
          </p>
        </div>

        {/* Loading */}
        {isLoading && <div className="flex items-center gap-2 text-xs text-white/40"><span className="h-3 w-3 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />Loading…</div>}

        {/* Journey list */}
        {journeys.length > 0 && (
          <ul className="flex flex-col gap-2">
            {journeys.map((journey) => {
              const isExpanded = expandedJourney === journey.name;
              return (
                <li key={journey.name}>
                  <div className={`rounded-2xl border transition-all ${isExpanded ? 'border-blue-500/30 bg-blue-500/5' : 'border-white/10 bg-white/5'}`}>
                    {/* Journey header */}
                    <button
                      type="button"
                      onClick={() => toggleJourney(journey.name)}
                      className="flex w-full min-h-[56px] items-center justify-between px-4 py-3 text-left"
                    >
                      <div className="flex items-center gap-3 min-w-0">
                        <div className="flex shrink-0">
                          {[...new Set(journey.legs.map((l) => l.vehicleType))].slice(0, 3).map((vt) => (
                            <span key={vt} className="text-lg">{ROUTE_VEHICLE_ICONS[vt] ?? '🚌'}</span>
                          ))}
                        </div>
                        <div className="min-w-0">
                          <p className="truncate text-sm font-semibold text-white">{journey.name}</p>
                          <p className="text-xs text-white/40">
                            {journey.legs.length === 1
                              ? `${ROUTE_VEHICLE_LABELS[journey.legs[0].vehicleType] ?? journey.legs[0].vehicleType} · ${journey.legs[0].waypoints.length} stops`
                              : `${journey.legs.length} legs`}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center gap-2 shrink-0 ml-2">
                        <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold capitalize ${journey.status === 'verified' ? 'bg-emerald-500/20 text-emerald-400' : 'bg-amber-500/20 text-amber-400'}`}>
                          {journey.status}
                        </span>
                        <svg className={`h-4 w-4 text-white/30 transition-transform duration-200 ${isExpanded ? 'rotate-180' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                          <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
                        </svg>
                      </div>
                    </button>

                    {/* Expanded legs */}
                    {isExpanded && (
                      <div className="border-t border-blue-500/20 px-3 pb-3 pt-2 flex flex-col gap-2">
                        {journey.legs.map((leg, legIdx) => {
                          const legLabel = leg.name.match(/Leg\s+\d+\s*\(([^)]+)\)/i)?.[1]?.trim()
                            ?? `${ROUTE_VEHICLE_ICONS[leg.vehicleType] ?? ''} ${ROUTE_VEHICLE_LABELS[leg.vehicleType] ?? leg.vehicleType}`;
                          return (
                            <div key={leg.id} className="flex items-center gap-3 rounded-xl border border-white/10 bg-white/5 px-3 py-2.5">
                              <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-blue-600 text-[10px] font-bold text-white">{legIdx + 1}</span>
                              <span className="text-base shrink-0">{ROUTE_VEHICLE_ICONS[leg.vehicleType] ?? '🚌'}</span>
                              <div className="flex-1 min-w-0">
                                <p className="text-xs font-medium text-white truncate">{legLabel}</p>
                                <p className="text-[11px] text-white/40">{leg.waypoints.length} stops</p>
                              </div>
                              {leg.vehicleType !== 'walk' && (
                                <span className="shrink-0 rounded-lg bg-blue-500/10 px-2 py-1 text-xs font-semibold text-blue-400">₱{leg.baseFare}</span>
                              )}
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                </li>
              );
            })}
          </ul>
        )}

        {!isLoading && journeys.length === 0 && (
          <p className="text-center text-sm text-white/40 py-6">
            {search ? `No routes matching "${search}"` : 'No routes yet.'}
          </p>
        )}
      </div>
    );
  }

  // Default: menu
  return (
    <div className="flex flex-col gap-3">
      <div>
        <h2 className="text-base font-bold text-white">Routes</h2>
        <p className="mt-0.5 text-xs text-white/50">Community-sourced transit map</p>
      </div>
      {[
        { label: 'Browse all routes', sub: 'Jeepney, UV Express, Bus, Tricycle', icon: '🗺️', action: () => setView('browse') },
        { label: 'Plot a new route', sub: 'Add a missing route to the map', icon: '✏️', action: () => setView('create') },
      ].map((item) => (
        <button key={item.label} onClick={item.action}
          className="flex min-h-[64px] items-center gap-3 rounded-2xl border border-white/10 bg-white/5 px-4 py-3 text-left transition hover:bg-white/10 active:scale-[0.98]">
          <span className="text-2xl">{item.icon}</span>
          <div className="flex-1">
            <p className="text-sm font-semibold text-white">{item.label}</p>
            <p className="text-xs text-white/40">{item.sub}</p>
          </div>
          <svg className="h-4 w-4 text-white/20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" /></svg>
        </button>
      ))}
    </div>
  );
}

// ─── Profile tab ──────────────────────────────────────────────────────────────

function ProfileTab({ onLogout }: { onLogout: () => void }) {
  const { user } = useMe();
  const initial = user?.displayName?.charAt(0).toUpperCase() ?? 'C';

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-4 rounded-2xl border border-white/10 bg-white/5 p-4">
        <div className="flex h-14 w-14 flex-shrink-0 items-center justify-center rounded-full bg-blue-600 text-xl font-bold text-white ring-4 ring-blue-500/20">{initial}</div>
        <div className="min-w-0">
          <p className="truncate text-base font-bold text-white">{user?.displayName ?? '—'}</p>
          <p className="truncate text-sm text-white/50">{user?.email ?? '—'}</p>
          <span className="mt-1 inline-block rounded-full bg-blue-500/20 px-2.5 py-0.5 text-[11px] font-semibold text-blue-300">Commuter</span>
        </div>
      </div>
      <div className="rounded-2xl border border-white/10 bg-white/5 divide-y divide-white/10">
        {[
          { label: 'Account type', value: 'Commuter' },
          { label: 'Language', value: user?.languagePreference === 'fil' ? 'Filipino' : 'English' },
        ].map((row) => (
          <div key={row.label} className="flex items-center justify-between px-4 py-3">
            <span className="text-sm text-white/50">{row.label}</span>
            <span className="text-sm font-medium text-white">{row.value}</span>
          </div>
        ))}
      </div>
      <button onClick={onLogout}
        className="flex min-h-[48px] w-full items-center justify-center gap-2 rounded-2xl border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm font-semibold text-red-400 transition hover:bg-red-500/20 active:scale-[0.98]">
        <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" /></svg>
        Log Out
      </button>
    </div>
  );
}

// ─── Nav config ───────────────────────────────────────────────────────────────

const navItems: { id: Tab; label: string; icon: React.ReactNode }[] = [
  {
    id: 'home', label: 'Home',
    icon: <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" /></svg>,
  },
  {
    id: 'routes', label: 'Routes',
    icon: <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" /></svg>,
  },
  {
    id: 'fare', label: 'Fare',
    icon: <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>,
  },
  {
    id: 'profile', label: 'Profile',
    icon: <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" /></svg>,
  },
];

// ─── Main component ───────────────────────────────────────────────────────────

export function CommuterDashboard() {
  const { user, isLoading } = useMe();
  const { logout } = useLogout();
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<Tab>('home');

  async function handleLogout() {
    await logout();
    router.push('/login');
  }

  const firstName = user?.displayName?.split(' ')[0] ?? 'there';
  const initial = user?.displayName?.charAt(0).toUpperCase() ?? 'C';
  const hour = new Date().getHours();
  const greeting = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';

  const tabTitles: Record<Tab, { title: string; sub: string }> = {
    home:    { title: `${greeting}, ${firstName} 👋`, sub: 'What do you need today?' },
    routes:  { title: 'Routes', sub: 'Community transit map' },
    fare:    { title: 'Fare Calculator', sub: 'LTFRB-compliant rates' },
    profile: { title: 'Profile', sub: 'Your account' },
  };

  return (
    <div className="fixed inset-0 flex flex-col bg-slate-950 text-white">

      {/* Top bar */}
      <header className="flex-none border-b border-white/10 bg-slate-950 px-4 py-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-xl bg-blue-600">
              <svg className="h-4 w-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
            </div>
            <div>
              <p className="text-sm font-bold text-white leading-none">{tabTitles[activeTab].title}</p>
              <p className="text-[11px] text-white/40 mt-0.5">{tabTitles[activeTab].sub}</p>
            </div>
          </div>
          {!isLoading && (
            <button
              onClick={() => setActiveTab(activeTab === 'profile' ? 'home' : 'profile')}
              className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-full bg-blue-600 text-sm font-bold text-white ring-2 ring-blue-500/30"
              aria-label="Open profile"
            >
              {initial}
            </button>
          )}
        </div>
      </header>

      {/* Scrollable content */}
      <main className="flex-1 overflow-y-auto px-4 py-4">
        {activeTab === 'home' && (
          <HomeTab
            onGoFare={() => setActiveTab('fare')}
            onGoRoutes={() => setActiveTab('routes')}
          />
        )}
        {activeTab === 'routes' && <RoutesTab />}
        {activeTab === 'fare' && <FareTab />}
        {activeTab === 'profile' && <ProfileTab onLogout={handleLogout} />}
      </main>

      {/* Bottom nav */}
      <nav className="flex-none border-t border-white/10 bg-slate-950">
        <div className="flex items-center justify-around px-2 py-2">
          {navItems.map((item) => {
            const isActive = activeTab === item.id;
            return (
              <button
                key={item.id}
                onClick={() => setActiveTab(item.id)}
                className={`flex min-h-[44px] min-w-[44px] flex-col items-center justify-center gap-1 rounded-xl px-3 py-2 transition ${
                  isActive ? 'text-blue-400' : 'text-white/40 hover:text-white/70'
                }`}
                aria-label={item.label}
                aria-current={isActive ? 'page' : undefined}
              >
                {item.icon}
                <span className="text-[10px] font-medium">{item.label}</span>
              </button>
            );
          })}
        </div>
      </nav>
    </div>
  );
}
