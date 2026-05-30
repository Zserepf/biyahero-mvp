'use client';

/**
 * Route list query hook — GET /v1/routes?bbox_sw_lat=...
 * Fetches all routes within the Philippines bounding box on mount,
 * then refines by map viewport on pan/zoom.
 * Requirements: 1.2
 */

import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import type { BboxQuery, RouteDto } from '../types';

export const ROUTES_QUERY_KEY = ['routes'] as const;

// Full Philippines bounding box — used for initial load so all routes appear
const PH_BBOX: BboxQuery = {
  bboxSwLat: 4.5,
  bboxSwLng: 116.0,
  bboxNeLat: 21.5,
  bboxNeLng: 127.0,
};

interface ApiWaypoint {
  lat: number;
  lng: number;
  position: number;
  name?: string | null;
}

interface ApiRouteItem {
  id: string;
  name: string;
  vehicleType: string;
  status: string;
  baseFare: number;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
  waypointCount: number;
  waypoints: ApiWaypoint[];
}

interface ApiListResponse {
  routes: ApiRouteItem[];
}

function mapApiRoute(r: ApiRouteItem): RouteDto {
  return {
    id: r.id,
    ownerId: r.createdBy ?? '',
    name: r.name,
    vehicleType: r.vehicleType as RouteDto['vehicleType'],
    baseFare: r.baseFare,
    status: r.status as RouteDto['status'],
    waypoints: (r.waypoints ?? []).map((wp) => ({
      lat: wp.lat,
      lng: wp.lng,
      position: wp.position,
      name: wp.name ?? undefined,
    })),
    voteCounts: { stillAccurate: 0, noLongerAccurate: 0 },
    createdAt: r.createdAt,
    updatedAt: r.updatedAt,
  };
}

async function fetchRoutes(bbox: BboxQuery): Promise<RouteDto[]> {
  const response = await apiClient.get<ApiListResponse>(
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
  return (response.data.routes ?? []).map(mapApiRoute);
}

export function useRouteList(bbox: BboxQuery | null) {
  // Use the provided bbox, or fall back to full Philippines bbox so routes
  // always appear even before the user pans the map.
  const effectiveBbox = bbox ?? PH_BBOX;

  return useQuery<RouteDto[]>({
    queryKey: [...ROUTES_QUERY_KEY, effectiveBbox],
    queryFn: () => fetchRoutes(effectiveBbox),
    staleTime: 30_000,
  });
}
