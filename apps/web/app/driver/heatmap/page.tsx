'use client';

/**
 * /driver/heatmap — Real-time demand heatmap for drivers
 * Requirements: 4.2, 4.3, 4.6
 */

import dynamic from 'next/dynamic';

const DriverHeatmapPage = dynamic(
  () =>
    import('@/features/heatmap/driver-heatmap/DriverHeatmapPage').then(
      (mod) => mod.DriverHeatmapPage,
    ),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-screen items-center justify-center bg-slate-900">
        <div className="flex flex-col items-center gap-3">
          <span className="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
          <p className="text-sm text-white/50">Loading map…</p>
        </div>
      </div>
    ),
  },
);

export default function DriverHeatmapPageRoute() {
  return (
    <div className="h-screen w-full overflow-hidden">
      <DriverHeatmapPage />
    </div>
  );
}
