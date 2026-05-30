'use client';

/**
 * RouteListPage — Browse community routes.
 *
 * Map starts clean (no pins). Routes are listed as journey names only.
 * Tapping a journey expands it to show its legs AND zooms the map to fit
 * all waypoints across all legs of that journey.
 *
 * Requirements: 1.2
 */

import { useState, useCallback, useMemo } from 'react';
import Link from 'next/link';
import dynamic from 'next/dynamic';
import { useRouteList } from './useRouteList';
import { ThemeToggle } from '@/shared/components/ThemeToggle';
import type { BboxQuery, RouteDto, Waypoint } from '../types';

const RouteMap = dynamic(
  () => import('../components/RouteMap').then((m) => m.RouteMap),
  { ssr: false, loading: () => <div className="h-[300px] rounded-2xl bg-white/5 animate-pulse" /> },
);

// ─── Constants ────────────────────────────────────────────────────────────────

const VEHICLE_ICONS: Record<string, string> = {
  jeepney:    '🚌',
  bus:        '🚍',
  uv_express: '🚐',
  tricycle:   '🛺',
  walk:       '🚶',
};

const VEHICLE_LABELS: Record<string, string> = {
  jeepney:    'Jeepney',
  bus:        'Bus',
  uv_express: 'UV Express',
  tricycle:   'Tricycle',
  walk:       'Walk',
};

const STATUS_STYLES: Record<string, string> = {
  verified:   'bg-emerald-500/20 text-emerald-400',
  unverified: 'bg-amber-500/20 text-amber-400',
};

// ─── Journey grouping ─────────────────────────────────────────────────────────

function extractJourneyName(routeName: string): string {
  return routeName.replace(/\s*—\s*Leg\s+\d+.*$/i, '').trim();
}

interface Journey {
  name: string;
  legs: RouteDto[];
  status: string;
}

function groupIntoJourneys(routes: RouteDto[]): Journey[] {
  const map = new Map<string, RouteDto[]>();
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

// Extract leg label from route name: "Leg 1 (🚌 Jeepney)" or fallback
function extractLegLabel(routeName: string): string | null {
  const match = routeName.match(/Leg\s+\d+\s*\(([^)]+)\)/i);
  return match ? match[1].trim() : null;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function RouteListPage() {
  const [bbox, setBbox] = useState<BboxQuery | null>(null);
  const [search, setSearch] = useState('');
  const [expandedJourney, setExpandedJourney] = useState<string | null>(null);

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

  const journeys = useMemo(() => {
    const all = routes ?? [];
    const filtered = search
      ? all.filter((r) =>
          extractJourneyName(r.name).toLowerCase().includes(search.toLowerCase()),
        )
      : all;
    return groupIntoJourneys(filtered);
  }, [routes, search]);

  // Collect ALL waypoints from the expanded journey's legs for the map
  const mapWaypoints = useMemo((): Waypoint[] => {
    if (!expandedJourney) return [];
    const journey = journeys.find((j) => j.name === expandedJourney);
    if (!journey) return [];

    // Merge all legs' waypoints with an offset so they all render
    let offset = 0;
    return journey.legs.flatMap((leg) => {
      const wps = leg.waypoints.map((wp) => ({
        ...wp,
        position: wp.position + offset,
      }));
      offset += leg.waypoints.length;
      return wps;
    });
  }, [expandedJourney, journeys]);

  const toggleJourney = (name: string) => {
    setExpandedJourney((prev) => (prev === name ? null : name));
  };

  const expandedJourneyData = journeys.find((j) => j.name === expandedJourney) ?? null;

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-indigo-50 dark:from-slate-900 dark:via-blue-950 dark:to-slate-900">
      {/* Header */}
      <header className="sticky top-0 z-50 border-b border-black/10 dark:border-white/10 bg-white/80 dark:bg-slate-900/80 backdrop-blur-md">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <div className="flex items-center gap-4">
            <Link
              href="/"
              className="flex h-9 w-9 items-center justify-center rounded-xl bg-gray-100 dark:bg-white/10 text-gray-700 dark:text-white transition hover:bg-gray-200 dark:hover:bg-white/20"
              aria-label="Back to home"
            >
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
              </svg>
            </Link>
            <div>
              <h1 className="text-base font-bold text-gray-900 dark:text-white leading-none">Browse Routes</h1>
              <p className="mt-0.5 text-xs text-gray-500 dark:text-white/50">Community transit map</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <ThemeToggle />
            <Link
              href="/commuter/routes/create"
              className="flex items-center gap-2 rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white shadow-lg shadow-blue-500/20 transition hover:bg-blue-500"
            >
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
              </svg>
              Plot Route
            </Link>
          </div>
        </div>
      </header>

      <div className="mx-auto max-w-5xl px-4 py-5 space-y-4">
        {/* Search */}
        <div className="relative">
          <svg className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400 dark:text-white/30" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
          <input
            type="text"
            placeholder="Search routes…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="min-h-[44px] w-full rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 py-2.5 pl-10 pr-4 text-sm text-gray-900 dark:text-white placeholder:text-gray-400 dark:placeholder:text-white/30 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition"
          />
        </div>

        {/* Map — clean slate until a journey is selected */}
        <div className="overflow-hidden rounded-2xl border border-gray-200 dark:border-white/10">
          <RouteMap
            waypoints={mapWaypoints}
            editable={false}
            onBoundsChange={handleBoundsChange}
            height="300px"
            scrollWheelZoom={false}
          />
          {!expandedJourney && (
            <p className="border-t border-gray-100 dark:border-white/5 bg-white/60 dark:bg-slate-900/60 px-3 py-1.5 text-center text-[11px] text-gray-400 dark:text-white/30">
              Tap a journey below to see it on the map
            </p>
          )}
          {expandedJourney && (
            <p className="border-t border-gray-100 dark:border-white/5 bg-white/60 dark:bg-slate-900/60 px-3 py-1.5 text-center text-[11px] text-blue-500 dark:text-blue-400">
              Showing: {expandedJourney}
            </p>
          )}
        </div>

        {/* Loading */}
        {isLoading && (
          <div className="flex items-center gap-2 text-sm text-gray-400 dark:text-white/40">
            <span className="h-4 w-4 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
            Loading routes…
          </div>
        )}

        {/* Journey list */}
        {journeys.length > 0 && (
          <div className="space-y-2">
            <p className="text-xs font-medium uppercase tracking-widest text-gray-400 dark:text-white/30">
              {journeys.length} journey{journeys.length !== 1 ? 's' : ''}
            </p>

            <ul className="flex flex-col gap-2">
              {journeys.map((journey) => {
                const isExpanded = expandedJourney === journey.name;

                return (
                  <li key={journey.name}>
                    <div className={`rounded-2xl border transition-all ${
                      isExpanded
                        ? 'border-blue-500/30 bg-blue-500/5 dark:bg-blue-500/5'
                        : 'border-gray-200 dark:border-white/10 bg-white dark:bg-white/5'
                    }`}>

                      {/* Journey header — tap to expand/collapse */}
                      <button
                        type="button"
                        onClick={() => toggleJourney(journey.name)}
                        className="flex w-full min-h-[60px] items-center justify-between px-4 py-3 text-left"
                        aria-expanded={isExpanded}
                      >
                        <div className="flex items-center gap-3 min-w-0">
                          {/* Vehicle icons for all legs */}
                          <div className="flex shrink-0">
                            {[...new Set(journey.legs.map((l) => l.vehicleType))]
                              .slice(0, 3)
                              .map((vt) => (
                                <span key={vt} className="text-xl">{VEHICLE_ICONS[vt] ?? '🚌'}</span>
                              ))}
                          </div>
                          <div className="min-w-0">
                            <p className="text-sm font-semibold text-gray-900 dark:text-white truncate">
                              {journey.name}
                            </p>
                            <p className="text-xs text-gray-500 dark:text-white/40">
                              {journey.legs.length === 1
                                ? `${VEHICLE_LABELS[journey.legs[0].vehicleType] ?? journey.legs[0].vehicleType} · ${journey.legs[0].waypoints.length} stops`
                                : `${journey.legs.length} legs`}
                            </p>
                          </div>
                        </div>
                        <div className="flex items-center gap-2 shrink-0 ml-2">
                          <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold capitalize ${STATUS_STYLES[journey.status] ?? STATUS_STYLES.unverified}`}>
                            {journey.status}
                          </span>
                          <svg
                            className={`h-4 w-4 text-gray-400 dark:text-white/30 transition-transform duration-200 ${isExpanded ? 'rotate-180' : ''}`}
                            fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}
                          >
                            <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
                          </svg>
                        </div>
                      </button>

                      {/* Expanded legs */}
                      {isExpanded && (
                        <div className="border-t border-blue-500/20 px-4 pb-4 pt-3 flex flex-col gap-2">
                          {journey.legs.map((leg, legIdx) => {
                            const legLabel = extractLegLabel(leg.name)
                              ?? `${VEHICLE_ICONS[leg.vehicleType] ?? ''} ${VEHICLE_LABELS[leg.vehicleType] ?? leg.vehicleType}`;

                            return (
                              <div key={leg.id} className="flex items-center gap-3 rounded-xl border border-white/10 dark:border-white/5 bg-white/50 dark:bg-white/5 px-3 py-2.5">
                                {/* Leg number */}
                                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-blue-600 text-[10px] font-bold text-white">
                                  {legIdx + 1}
                                </span>

                                {/* Vehicle icon */}
                                <span className="text-lg shrink-0">
                                  {VEHICLE_ICONS[leg.vehicleType] ?? '🚌'}
                                </span>

                                {/* Label + fare */}
                                <div className="flex-1 min-w-0">
                                  <p className="text-sm font-medium text-gray-800 dark:text-white truncate">
                                    {legLabel}
                                  </p>
                                  <p className="text-xs text-gray-500 dark:text-white/40">
                                    {leg.waypoints.length} stops
                                  </p>
                                </div>

                                {/* Fare */}
                                {leg.vehicleType !== 'walk' && (
                                  <span className="shrink-0 rounded-lg bg-blue-500/10 px-2 py-1 text-xs font-semibold text-blue-600 dark:text-blue-400">
                                    ₱{leg.baseFare}
                                  </span>
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
          </div>
        )}

        {/* Empty state */}
        {!isLoading && journeys.length === 0 && (
          <div className="rounded-2xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 py-12 text-center">
            <p className="text-sm text-gray-400 dark:text-white/40">
              {search ? `No routes matching "${search}"` : 'No routes yet.'}
            </p>
            <Link
              href="/commuter/routes/create"
              className="mt-4 inline-flex items-center gap-2 rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-blue-500"
            >
              Be the first to plot one
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}
