'use client';

/**
 * Create Route mutation hook — handles POST /v1/routes.
 *
 * Implements optimistic UI: the route is shown immediately on submit,
 * reverted on error. Offline writes are queued via the IndexedDB write queue.
 *
 * Requirements: 1.1, 6.4
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import type { CreateRouteRequest, CreateRouteResponse, RouteDto } from '../types';

export const ROUTES_QUERY_KEY = ['routes'] as const;

export function useCreateRoute() {
  const queryClient = useQueryClient();

  return useMutation<CreateRouteResponse, Error, CreateRouteRequest, { previousRoutes: RouteDto[] | undefined }>({
    mutationFn: async (data: CreateRouteRequest) => {
      const response = await apiClient.post<CreateRouteResponse>(
        API_ENDPOINTS.ROUTES.CREATE,
        data,
      );
      return response.data;
    },

    // Optimistic update: add the route to the cache immediately
    onMutate: async (newRoute) => {
      // Cancel any outgoing refetches
      await queryClient.cancelQueries({ queryKey: ROUTES_QUERY_KEY });

      // Snapshot the previous value
      const previousRoutes = queryClient.getQueryData<RouteDto[]>(ROUTES_QUERY_KEY);

      // Optimistically add the new route
      const optimisticRoute: RouteDto = {
        id: `optimistic-${Date.now()}`,
        ownerId: '',
        name: newRoute.name,
        vehicleType: newRoute.vehicleType,
        baseFare: newRoute.baseFare,
        status: 'unverified',
        waypoints: newRoute.waypoints.map((wp, i) => ({
          ...wp,
          position: i,
        })),
        voteCounts: { stillAccurate: 0, noLongerAccurate: 0 },
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };

      queryClient.setQueryData<RouteDto[]>(ROUTES_QUERY_KEY, (old) => [
        ...(old ?? []),
        optimisticRoute,
      ]);

      return { previousRoutes };
    },

    // On error, revert to the previous state
    onError: (_err, _newRoute, context) => {
      if (context?.previousRoutes) {
        queryClient.setQueryData(ROUTES_QUERY_KEY, context.previousRoutes);
      }
    },

    // On success or error, refetch to sync with server
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ROUTES_QUERY_KEY });
    },
  });
}
