/**
 * /routes/{id} — Route detail page.
 * Requirements: 1.2, 1.5
 */

'use client';

import dynamic from 'next/dynamic';
import { useParams } from 'next/navigation';

const RouteDetailPage = dynamic(
  () =>
    import('@/features/routes/route-detail/RouteDetailPage').then(
      (mod) => mod.RouteDetailPage,
    ),
  { ssr: false, loading: () => <p className="p-4 text-sm text-gray-500">Loading route...</p> },
);

export default function RouteDetailPageRoute() {
  const params = useParams();
  const routeId = params.id as string;

  if (!routeId) {
    return <p className="p-4 text-sm text-red-600">Route not found.</p>;
  }

  return <RouteDetailPage routeId={routeId} />;
}
