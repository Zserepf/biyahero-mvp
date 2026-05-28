'use client';

/**
 * Registration hook — handles account creation via POST /v1/auth/registrations.
 *
 * On success, the user receives a verification email.
 * Requirements: 5.1
 */

import { useState, useCallback } from 'react';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import { ApiError } from '@/shared/types/api';
import type { RegisterRequest, RegisterResponse } from './types';

interface UseRegisterReturn {
  register: (data: RegisterRequest) => Promise<RegisterResponse>;
  isLoading: boolean;
  error: string | null;
}

export function useRegister(): UseRegisterReturn {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const register = useCallback(async (data: RegisterRequest): Promise<RegisterResponse> => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await apiClient.post<RegisterResponse>(
        API_ENDPOINTS.AUTH.REGISTER,
        data,
      );

      return response.data;
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

  return { register, isLoading, error };
}
