'use client';

/**
 * /commuter/waiting — "I'm Waiting Here" demand ping page
 * Requirements: 4.1, 4.5
 */

import dynamic from 'next/dynamic';

const CommuterHeatmapPage = dynamic(
  () =>
    import('@/features/heatmap/commuter-heatmap/CommuterHeatmapPage').then(
      (mod) => mod.CommuterHeatmapPage,
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

export default function CommuterWaitingPage() {
  return (
    <div className="h-screen w-full overflow-hidden">
      <CommuterHeatmapPage />
    </div>
  );
}
