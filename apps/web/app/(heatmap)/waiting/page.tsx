/**
 * /heatmap/waiting — Commuter "I'm waiting here" page.
 * Requirements: 4.1, 4.5
 */

'use client';

import dynamic from 'next/dynamic';

// Leaflet requires window/document, so we lazy-load with SSR disabled
const CommuterHeatmapPage = dynamic(
  () =>
    import('@/features/heatmap/commuter-heatmap/CommuterHeatmapPage').then(
      (mod) => mod.CommuterHeatmapPage,
    ),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-screen items-center justify-center">
        <p className="text-sm text-gray-500">Loading map...</p>
      </div>
    ),
  },
);

export default function WaitingPageRoute() {
  return <CommuterHeatmapPage />;
}
