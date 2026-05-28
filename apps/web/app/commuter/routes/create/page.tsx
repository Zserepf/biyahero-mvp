'use client';

/**
 * /commuter/routes/create — Plot a new community route
 * Requirements: 1.1
 */

import dynamic from 'next/dynamic';

const CreateRoutePage = dynamic(
  () => import('@/features/routes/create-route/CreateRoutePage').then((mod) => mod.CreateRoutePage),
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

export default function CommuterCreateRoutePage() {
  return <CreateRoutePage />;
}
