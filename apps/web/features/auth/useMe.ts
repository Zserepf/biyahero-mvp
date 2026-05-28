'use client';

/**
 * Current user hook — fetches the authenticated user's profile via GET /v1/auth/me.
 *
 * Returns null when not authenticated. Used by AuthGuard and profile displays.
 */

import { useState, useEffect, useCallback } from 'react';
import { apiClient, ACCESS_TOKEN_KEY } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import { ApiError } from '@/shared/types/api';
import type { MeResponse } from './types';

interface UseMeReturn {
  user: MeResponse | null;
  isLoading: boolean;
  error: string | null;
  refetch: () => Promise<void>;
}

export function useMe(): UseMeReturn {
  const [user, setUser] = useState<MeResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchMe = useCallback(async () => {
    const token = typeof window !== 'undefined'
      ? localStorage.getItem(ACCESS_TOKEN_KEY)
      : null;

    if (!token) {
      setUser(null);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const response = await apiClient.get<MeResponse>(API_ENDPOINTS.AUTH.ME);
      setUser(response.data);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        // Token expired or invalid — clear it
        localStorage.removeItem(ACCESS_TOKEN_KEY);
        setUser(null);
      } else {
        setError(err instanceof ApiError ? err.message : 'errors.generic');
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchMe();
  }, [fetchMe]);

  return { user, isLoading, error, refetch: fetchMe };
}
