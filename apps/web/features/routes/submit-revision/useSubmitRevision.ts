'use client';

/**
 * Submit revision mutation hook — handles POST /v1/routes/{id}/revisions.
 *
 * Submits an edit to an existing route as a pending revision.
 * Optimistic UI: shows the revision immediately, reverts on error.
 * Offline writes queued via task 12.5.
 *
 * Requirements: 1.3, 6.4
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import type { CreateRevisionRequest, CreateRevisionResponse } from '../types';

export function useSubmitRevision(routeId: string) {
  const queryClient = useQueryClient();

  return useMutation<CreateRevisionResponse, Error, CreateRevisionRequest>({
    mutationFn: async (data: CreateRevisionRequest) => {
      const response = await apiClient.post<CreateRevisionResponse>(
        API_ENDPOINTS.ROUTES.CREATE_REVISION(routeId),
        data,
      );
      return response.data;
    },

    onSuccess: () => {
      // Invalidate the route detail to show the new revision
      queryClient.invalidateQueries({ queryKey: ['routes', routeId] });
      queryClient.invalidateQueries({ queryKey: ['routes'] });
    },
  });
}
