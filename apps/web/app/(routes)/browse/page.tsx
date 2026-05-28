/**
 * /routes/browse — Browse routes on a map page.
 * Requirements: 1.2
 */

'use client';

import dynamic from 'next/dynamic';

// Leaflet requires window/document, so we lazy-load with SSR disabled
const RouteListPage = dynamic(
  () =>
    import('@/features/routes/route-list/RouteListPage').then(
      (mod) => mod.RouteListPage,
    ),
  { ssr: false, loading: () => <p className="p-4 text-sm text-gray-500">Loading map...</p> },
);

export default function BrowseRoutesPageRoute() {
  return <RouteListPage />;
}
