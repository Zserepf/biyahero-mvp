'use client';

/**
 * Token refresh hook — POST /v1/auth/sessions/:refresh.
 *
 * Issues a new access token using the stored refresh token.
 * Typically called by the API client interceptor on 401 responses.
 * Requirements: 5.3
 */

import { useState, useCallback } from 'react';
import { apiClient, ACCESS_TOKEN_KEY } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import { ApiError } from '@/shared/types/api';
import type { RefreshResponse } from './types';

const REFRESH_TOKEN_KEY = 'biyahero_refresh_token';

interface UseRefreshReturn {
  refresh: () => Promise<RefreshResponse>;
  isLoading: boolean;
  error: string | null;
}

export function useRefresh(): UseRefreshReturn {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async (): Promise<RefreshResponse> => {
    setIsLoading(true);
    setError(null);

    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    if (!refreshToken) {
      const err = 'errors.noRefreshToken';
      setError(err);
      setIsLoading(false);
      throw new Error(err);
    }

    try {
      const response = await apiClient.post<RefreshResponse>(
        API_ENDPOINTS.AUTH.REFRESH,
        { refreshToken },
      );

      const { accessToken, refreshToken: newRefreshToken } = response.data;

      // Update stored tokens
      localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
      localStorage.setItem(REFRESH_TOKEN_KEY, newRefreshToken);

      return response.data;
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('errors.generic');
      }
      // Clear tokens on refresh failure — session is invalid
      localStorage.removeItem(ACCESS_TOKEN_KEY);
      localStorage.removeItem(REFRESH_TOKEN_KEY);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  return { refresh, isLoading, error };
}
