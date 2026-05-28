'use client';

/**
 * RouteListPage — Browse community routes on a map.
 * Requirements: 1.2
 */

import { useState, useCallback } from 'react';
import Link from 'next/link';
import { RouteMap } from '../components/RouteMap';
import { useRouteList } from './useRouteList';
import { ThemeToggle } from '@/shared/components/ThemeToggle';
import type { BboxQuery, RouteDto } from '../types';

const VEHICLE_ICONS: Record<string, string> = {
  Jeepney: '🚌',
  Bus: '🚍',
  UV_Express: '🚐',
  Tricycle: '🛺',
};

const STATUS_STYLES: Record<string, string> = {
  verified:   'bg-emerald-500/20 text-emerald-400 ring-1 ring-emerald-500/30',
  unverified: 'bg-amber-500/20 text-amber-400 ring-1 ring-amber-500/30',
};

interface RouteListPageProps {
  onRouteSelect?: (route: RouteDto) => void;
}

export function RouteListPage({ onRouteSelect }: RouteListPageProps) {
  const [bbox, setBbox] = useState<BboxQuery | null>(null);
  const [search, setSearch] = useState('');
  const [vehicleFilter, setVehicleFilter] = useState('all');
  const { data: routes, isLoading } = useRouteList(bbox);

  const handleBoundsChange = useCallback(
    (bounds: { swLat: number; swLng: number; neLat: number; neLng: number }) => {
      setBbox({ bboxSwLat: bounds.swLat, bboxSwLng: bounds.swLng, bboxNeLat: bounds.neLat, bboxNeLng: bounds.neLng });
    },
    [],
  );

  const allWaypoints =
    routes?.flatMap((route) =>
      route.waypoints.map((wp) => ({
        ...wp,
        name: `${route.name} — ${wp.name || `Stop ${wp.position + 1}`}`,
      })),
    ) ?? [];

  const filtered = (routes ?? []).filter((r) => {
    const matchSearch = !search || r.name.toLowerCase().includes(search.toLowerCase());
    const matchVehicle = vehicleFilter === 'all' || r.vehicleType === vehicleFilter;
    return matchSearch && matchVehicle;
  });

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
              <p className="mt-0.5 text-xs text-gray-500 dark:text-white/50">Community-sourced transit map</p>
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
        {/* Search + filter bar */}
        <div className="flex flex-col gap-3 sm:flex-row">
          <div className="relative flex-1">
            <svg className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400 dark:text-white/30" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
            <input
              type="text"
              placeholder="Search routes by name…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="min-h-[44px] w-full rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 py-2.5 pl-10 pr-4 text-sm text-gray-900 dark:text-white placeholder:text-gray-400 dark:placeholder:text-white/30 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition"
            />
          </div>
          <select
            value={vehicleFilter}
            onChange={(e) => setVehicleFilter(e.target.value)}
            className="min-h-[44px] rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-slate-800 px-4 py-2.5 text-sm text-gray-900 dark:text-white focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition sm:w-44"
          >
            <option value="all">All Vehicles</option>
            <option value="Jeepney">Jeepney</option>
            <option value="Bus">Bus</option>
            <option value="UV_Express">UV Express</option>
            <option value="Tricycle">Tricycle</option>
          </select>
        </div>

        {/* Map */}
        <div className="overflow-hidden rounded-2xl border border-gray-200 dark:border-white/10">
          <RouteMap
            waypoints={allWaypoints}
            editable={false}
            onBoundsChange={handleBoundsChange}
            height="380px"
          />
        </div>

        {/* Loading */}
        {isLoading && (
          <div className="flex items-center gap-2 text-sm text-gray-400 dark:text-white/40" aria-live="polite">
            <span className="h-4 w-4 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
            Loading routes…
          </div>
        )}

        {/* Route list */}
        {filtered.length > 0 && (
          <div className="space-y-2">
            <p className="text-xs font-medium uppercase tracking-widest text-gray-400 dark:text-white/30">
              {filtered.length} route{filtered.length !== 1 ? 's' : ''} in view
            </p>
            <ul className="flex flex-col gap-2" aria-label="Routes in current map view">
              {filtered.map((route) => (
                <li key={route.id}>
                  <button
                    type="button"
                    onClick={() => onRouteSelect?.(route)}
                    className="group flex w-full min-h-[56px] items-center justify-between rounded-2xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 px-4 py-3 text-left transition hover:border-gray-300 dark:hover:border-white/20 hover:bg-gray-50 dark:hover:bg-white/10 focus:outline-none focus:ring-2 focus:ring-blue-500/30"
                    aria-label={`View route: ${route.name}`}
                  >
                    <div className="flex items-center gap-3">
                      <span className="text-xl" aria-hidden="true">
                        {VEHICLE_ICONS[route.vehicleType] ?? '🚌'}
                      </span>
                      <div>
                        <p className="text-sm font-semibold text-gray-900 dark:text-white">{route.name}</p>
                        <p className="text-xs text-gray-500 dark:text-white/40">
                          {route.vehicleType.replace('_', ' ')} • {route.waypoints.length} stops
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className={`rounded-full px-2.5 py-0.5 text-[10px] font-semibold capitalize ${STATUS_STYLES[route.status] ?? STATUS_STYLES.unverified}`}>
                        {route.status}
                      </span>
                      <div className="flex items-center gap-2 text-xs">
                        <span className="text-emerald-500 dark:text-emerald-400">✓ {route.voteCounts.stillAccurate}</span>
                        <span className="text-red-500 dark:text-red-400">✗ {route.voteCounts.noLongerAccurate}</span>
                      </div>
                      <svg className="h-4 w-4 text-gray-300 dark:text-white/20 transition group-hover:text-gray-400 dark:group-hover:text-white/50" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
                      </svg>
                    </div>
                  </button>
                </li>
              ))}
            </ul>
          </div>
        )}

        {routes && routes.length === 0 && !isLoading && (
          <div className="rounded-2xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 py-12 text-center">
            <p className="text-sm text-gray-400 dark:text-white/40">No routes in this area yet.</p>
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
