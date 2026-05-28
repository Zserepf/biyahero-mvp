'use client';

/**
 * Suspend user mutation hook — POST /v1/admin/users/{id}/:suspend.
 *
 * Suspends a user account. Requires Super Admin role.
 * Requirements: 5.8
 */

import { useState, useCallback } from 'react';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import { ApiError } from '@/shared/types/api';
import type { SuspendUserResponse } from './types';

interface UseSuspendUserReturn {
  suspendUser: (userId: string) => Promise<void>;
  isLoading: boolean;
  error: string | null;
}

export function useSuspendUser(): UseSuspendUserReturn {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const suspendUser = useCallback(async (userId: string) => {
    setIsLoading(true);
    setError(null);

    try {
      await apiClient.post<SuspendUserResponse>(
        API_ENDPOINTS.ADMIN.SUSPEND_USER(userId),
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
  }, []);

  return { suspendUser, isLoading, error };
}
