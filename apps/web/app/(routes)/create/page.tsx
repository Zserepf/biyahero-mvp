/**
 * /routes/create — Create a new route page.
 * Requirements: 1.1
 */

'use client';

import dynamic from 'next/dynamic';

// Leaflet requires window/document, so we lazy-load with SSR disabled
const CreateRoutePage = dynamic(
  () =>
    import('@/features/routes/create-route/CreateRoutePage').then(
      (mod) => mod.CreateRoutePage,
    ),
  { ssr: false, loading: () => <p className="p-4 text-sm text-gray-500">Loading map...</p> },
);

export default function CreateRoutePageRoute() {
  return <CreateRoutePage />;
}
