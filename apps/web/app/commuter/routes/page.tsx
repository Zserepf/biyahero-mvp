'use client';

/**
 * /commuter/routes — Browse community routes (commuter + guest access)
 * Requirements: 1.2
 */

import dynamic from 'next/dynamic';

const RouteListPage = dynamic(
  () => import('@/features/routes/route-list/RouteListPage').then((mod) => mod.RouteListPage),
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

export default function CommuterRoutesPage() {
  return <RouteListPage />;
}
