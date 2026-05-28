/**
 * /routes/{id}/edit — Submit revision page.
 * Requirements: 1.3
 */

'use client';

import dynamic from 'next/dynamic';
import { useParams } from 'next/navigation';

const SubmitRevisionPage = dynamic(
  () =>
    import('@/features/routes/submit-revision/SubmitRevisionPage').then(
      (mod) => mod.SubmitRevisionPage,
    ),
  { ssr: false, loading: () => <p className="p-4 text-sm text-gray-500">Loading editor...</p> },
);

export default function EditRoutePageRoute() {
  const params = useParams();
  const routeId = params.id as string;

  if (!routeId) {
    return <p className="p-4 text-sm text-red-600">Route not found.</p>;
  }

  return <SubmitRevisionPage routeId={routeId} />;
}
