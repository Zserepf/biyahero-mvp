'use client';

/**
 * Fare calculation hook — handles POST /v1/fare/:calculate.
 *
 * Anonymous access (no auth required).
 * Requirements: 2.1, 2.5, 2.9
 */

import { useState, useCallback } from 'react';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import { ApiError } from '@/shared/types/api';
import type { FareCalculateRequest, FareCalculateResponse } from './types';

interface UseCalculateFareReturn {
  calculateFare: (data: FareCalculateRequest) => Promise<void>;
  result: FareCalculateResponse | null;
  isLoading: boolean;
  error: string | null;
  reset: () => void;
}

export function useCalculateFare(): UseCalculateFareReturn {
  const [result, setResult] = useState<FareCalculateResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const calculateFare = useCallback(async (data: FareCalculateRequest) => {
    setIsLoading(true);
    setError(null);
    setResult(null);

    try {
      const response = await apiClient.post<FareCalculateResponse>(
        API_ENDPOINTS.FARE.CALCULATE,
        data,
      );

      setResult(response.data);
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

  const reset = useCallback(() => {
    setResult(null);
    setError(null);
  }, []);

  return { calculateFare, result, isLoading, error, reset };
}
