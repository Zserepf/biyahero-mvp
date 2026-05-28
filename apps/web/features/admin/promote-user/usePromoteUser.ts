'use client';

/**
 * Promote user mutation hook — POST /v1/admin/users/{id}/:promote.
 *
 * Changes a user's role. Requires the acting Super Admin to re-enter
 * their own password as a 2FA confirmation before the role change is applied.
 * Requirements: 5.9, 5.11
 */

import { useState, useCallback } from 'react';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import { ApiError } from '@/shared/types/api';
import type { PromoteUserRequest, PromoteUserResponse } from './types';

interface UsePromoteUserReturn {
  promoteUser: (userId: string, data: PromoteUserRequest) => Promise<void>;
  isLoading: boolean;
  error: string | null;
}

export function usePromoteUser(): UsePromoteUserReturn {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const promoteUser = useCallback(
    async (userId: string, data: PromoteUserRequest) => {
      setIsLoading(true);
      setError(null);

      try {
        await apiClient.post<PromoteUserResponse>(
          API_ENDPOINTS.ADMIN.PROMOTE_USER(userId),
          data,
        );
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message);
        } else {
          setError('errors.generic');
        }
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    [],
  );

  return { promoteUser, isLoading, error };
}
