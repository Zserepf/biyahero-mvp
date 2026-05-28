'use client';

/**
 * Route detail query hook — handles GET /v1/routes/{id}.
 *
 * Fetches a single route with its waypoints for the detail view.
 * Requirements: 1.2
 */

import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import type { RouteDto } from '../types';

export function useRouteDetail(routeId: string | null) {
  return useQuery<RouteDto>({
    queryKey: ['routes', routeId],
    queryFn: async () => {
      const response = await apiClient.get<RouteDto>(
        API_ENDPOINTS.ROUTES.GET(routeId!),
      );
      return response.data;
    },
    enabled: routeId !== null && routeId.length > 0,
  });
}
