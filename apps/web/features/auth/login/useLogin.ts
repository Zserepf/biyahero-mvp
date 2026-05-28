'use client';

/**
 * Login mutation hook — POST /v1/auth/sessions.
 *
 * Stores JWT tokens in localStorage on success and syncs language preference.
 * Requirements: 5.3, 10.4
 */

import { useState, useCallback } from 'react';
import { apiClient, ACCESS_TOKEN_KEY } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import { useLanguagePreferenceStore } from '@/infrastructure/stores/language-preference-store';
import { ApiError } from '@/shared/types/api';
import type { LoginRequest, LoginResponse } from './types';

const REFRESH_TOKEN_KEY = 'biyahero_refresh_token';

interface UseLoginReturn {
  login: (data: LoginRequest) => Promise<void>;
  isLoading: boolean;
  error: string | null;
}

export function useLogin(): UseLoginReturn {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const syncFromServer = useLanguagePreferenceStore((s) => s.syncFromServer);

  const login = useCallback(
    async (data: LoginRequest) => {
      setIsLoading(true);
      setError(null);

      try {
        const response = await apiClient.post<LoginResponse>(
          API_ENDPOINTS.AUTH.LOGIN,
          data,
        );

        const { accessToken, refreshToken, user } = response.data;

        // Store tokens in localStorage
        localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
        localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);

        // Sync language preference from server (Req 10.4)
        if (user.languagePreference) {
          const { setLocale } = useLanguagePreferenceStore.getState();
          setLocale(user.languagePreference);
        }

        await syncFromServer();
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
    [syncFromServer],
  );

  return { login, isLoading, error };
}
