'use client';

/**
 * /driver/dashboard — Driver home screen (full-screen heatmap PWA shell)
 */

import dynamic from 'next/dynamic';

const DriverDashboard = dynamic(
  () => import('@/features/dashboard/driver/DriverDashboard').then((m) => m.DriverDashboard),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-screen items-center justify-center bg-slate-900">
        <span className="h-8 w-8 animate-spin rounded-full border-2 border-purple-500 border-t-transparent" />
      </div>
    ),
  },
);

export default function DriverDashboardPage() {
  return (
    <div className="h-screen w-full overflow-hidden">
      <DriverDashboard />
    </div>
  );
}
