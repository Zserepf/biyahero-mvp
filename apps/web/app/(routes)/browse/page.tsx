/**
 * /routes/browse — Browse routes on a map page.
 *
 * Enhanced layout with gradient header, polished map container,
 * and floating search panel overlay.
 *
 * Requirements: 1.2
 */

'use client';

import { useState } from 'react';
import dynamic from 'next/dynamic';

// Leaflet requires window/document, so we lazy-load with SSR disabled
const RouteListPage = dynamic(
  () =>
    import('@/features/routes/route-list/RouteListPage').then(
      (mod) => mod.RouteListPage,
    ),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-[500px] items-center justify-center rounded-2xl bg-gray-50">
        <div className="flex flex-col items-center gap-3">
          <div className="h-8 w-8 animate-spin rounded-full border-3 border-blue-600 border-t-transparent" />
          <p className="text-sm text-gray-500">Loading map...</p>
        </div>
      </div>
    ),
  },
);

export default function BrowseRoutesPageRoute() {
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedVehicle, setSelectedVehicle] = useState<string>('all');

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Gradient Header */}
      <header className="bg-gradient-to-r from-blue-600 via-blue-700 to-indigo-700 px-4 pb-16 pt-8 text-white shadow-lg">
        <div className="mx-auto max-w-5xl">
          <h1 className="text-3xl font-bold tracking-tight">Browse Routes</h1>
          <p className="mt-2 text-sm text-blue-100 opacity-90">
            Discover community-submitted transit routes across Metro Manila. Pan and zoom to explore.
          </p>
        </div>
      </header>

      {/* Main Content — overlaps header */}
      <div className="relative mx-auto -mt-10 max-w-5xl px-4 pb-8">
        {/* Floating Search/Filter Panel */}
        <div className="mb-4 rounded-xl bg-white p-4 shadow-lg ring-1 ring-gray-100">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
            {/* Search Input */}
            <div className="relative flex-1">
              <svg
                className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth={2}
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
                />
              </svg>
              <input
                type="text"
                placeholder="Search routes by name or area..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="min-h-[44px] w-full rounded-lg border border-gray-200 bg-gray-50 py-2.5 pl-10 pr-4 text-sm placeholder:text-gray-400 focus:border-blue-500 focus:bg-white focus:outline-none focus:ring-2 focus:ring-blue-200"
              />
            </div>

            {/* Vehicle Filter */}
            <select
              value={selectedVehicle}
              onChange={(e) => setSelectedVehicle(e.target.value)}
              className="min-h-[44px] rounded-lg border border-gray-200 bg-gray-50 px-3 py-2.5 text-sm text-gray-700 focus:border-blue-500 focus:bg-white focus:outline-none focus:ring-2 focus:ring-blue-200 sm:w-44"
            >
              <option value="all">All Vehicles</option>
              <option value="Jeepney">Jeepney</option>
              <option value="Bus">Bus</option>
              <option value="UV_Express">UV Express</option>
              <option value="Tricycle">Tricycle</option>
            </select>

            {/* Filter Badge */}
            <div className="flex items-center gap-2">
              <span className="inline-flex items-center gap-1 rounded-full bg-blue-50 px-3 py-1.5 text-xs font-medium text-blue-700">
                <svg className="h-3 w-3" fill="currentColor" viewBox="0 0 20 20">
                  <path
                    fillRule="evenodd"
                    d="M5.05 4.05a7 7 0 119.9 9.9L10 18.9l-4.95-4.95a7 7 0 010-9.9zM10 11a2 2 0 100-4 2 2 0 000 4z"
                    clipRule="evenodd"
                  />
                </svg>
                Metro Manila
              </span>
            </div>
          </div>
        </div>

        {/* Map Container */}
        <div className="overflow-hidden rounded-2xl bg-white shadow-lg ring-1 ring-gray-100">
          <RouteListPage />
        </div>
      </div>
    </div>
  );
}
