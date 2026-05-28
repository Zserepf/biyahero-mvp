'use client';

/**
 * Vote route mutation hook — handles POST /v1/routes/{id}/votes.
 *
 * Casts an accuracy vote (still_accurate / no_longer_accurate).
 * Optimistic UI: updates vote counts immediately, reverts on error.
 *
 * Requirements: 1.5
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import type { CastVoteRequest, CastVoteResponse, RouteDto } from '../types';

export function useVoteRoute(routeId: string) {
  const queryClient = useQueryClient();

  return useMutation<CastVoteResponse, Error, CastVoteRequest, { previousRoute: RouteDto | undefined }>({
    mutationFn: async (data: CastVoteRequest) => {
      const response = await apiClient.post<CastVoteResponse>(
        API_ENDPOINTS.ROUTES.VOTE(routeId),
        data,
      );
      return response.data;
    },

    // Optimistic update: increment the vote count immediately
    onMutate: async (newVote) => {
      await queryClient.cancelQueries({ queryKey: ['routes', routeId] });

      const previousRoute = queryClient.getQueryData<RouteDto>(['routes', routeId]);

      if (previousRoute) {
        const updatedRoute = { ...previousRoute };
        const counts = { ...updatedRoute.voteCounts };

        if (newVote.kind === 'still_accurate') {
          counts.stillAccurate += 1;
        } else {
          counts.noLongerAccurate += 1;
        }

        updatedRoute.voteCounts = counts;
        queryClient.setQueryData(['routes', routeId], updatedRoute);
      }

      return { previousRoute };
    },

    onError: (_err, _newVote, context) => {
      if (context?.previousRoute) {
        queryClient.setQueryData(['routes', routeId], context.previousRoute);
      }
    },

    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['routes', routeId] });
    },
  });
}
