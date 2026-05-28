'use client';

/**
 * Route list query hook — handles GET /v1/routes?bbox_sw_lat=...&bbox_sw_lng=...&bbox_ne_lat=...&bbox_ne_lng=...
 *
 * Fetches routes within a bounding box for the map view.
 * Requirements: 1.2
 */

import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import type { BboxQuery, RouteDto } from '../types';

export const ROUTES_QUERY_KEY = ['routes'] as const;

export function useRouteList(bbox: BboxQuery | null) {
  return useQuery<RouteDto[]>({
    queryKey: [...ROUTES_QUERY_KEY, bbox],
    queryFn: async () => {
      if (!bbox) return [];

      const response = await apiClient.get<{ routes: RouteDto[] }>(
        API_ENDPOINTS.ROUTES.LIST,
        {
          params: {
            bbox_sw_lat: bbox.bboxSwLat,
            bbox_sw_lng: bbox.bboxSwLng,
            bbox_ne_lat: bbox.bboxNeLat,
            bbox_ne_lng: bbox.bboxNeLng,
          },
        },
      );
      return response.data.routes;
    },
    enabled: bbox !== null,
    staleTime: 30_000, // 30 seconds before refetch
  });
}
