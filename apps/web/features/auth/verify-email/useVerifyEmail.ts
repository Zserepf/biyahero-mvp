'use client';

/**
 * Email verification hook — POST /v1/auth/email-verifications/:verify.
 *
 * Consumes the single-use verification token from the URL.
 * Requirements: 5.2
 */

import { useState, useCallback } from 'react';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import { ApiError } from '@/shared/types/api';
import type { VerifyEmailResponse } from './types';

interface UseVerifyEmailReturn {
  verifyEmail: (token: string) => Promise<VerifyEmailResponse>;
  isLoading: boolean;
  error: string | null;
  isSuccess: boolean;
}

export function useVerifyEmail(): UseVerifyEmailReturn {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSuccess, setIsSuccess] = useState(false);

  const verifyEmail = useCallback(async (token: string): Promise<VerifyEmailResponse> => {
    setIsLoading(true);
    setError(null);
    setIsSuccess(false);

    try {
      const response = await apiClient.post<VerifyEmailResponse>(
        API_ENDPOINTS.AUTH.VERIFY_EMAIL,
        { token },
      );
      setIsSuccess(true);
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

  return { verifyEmail, isLoading, error, isSuccess };
}
