'use client';

/**
 * Logout hook — DELETE /v1/auth/sessions/{id}.
 *
 * Calls the backend to revoke the session, then clears all stored tokens.
 */

import { useState, useCallback } from 'react';
import { apiClient, ACCESS_TOKEN_KEY } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';

const REFRESH_TOKEN_KEY = 'biyahero_refresh_token';
const SESSION_ID_KEY = 'biyahero_session_id';

interface UseLogoutReturn {
  logout: () => Promise<void>;
  isLoading: boolean;
}

export function useLogout(): UseLogoutReturn {
  const [isLoading, setIsLoading] = useState(false);

  const logout = useCallback(async () => {
    setIsLoading(true);

    try {
      const sessionId = localStorage.getItem(SESSION_ID_KEY);
      if (sessionId) {
        await apiClient.delete(API_ENDPOINTS.AUTH.LOGOUT(sessionId));
      }
    } catch {
      // Best-effort logout — clear tokens regardless of server response
    } finally {
      // Always clear local tokens
      localStorage.removeItem(ACCESS_TOKEN_KEY);
      localStorage.removeItem(REFRESH_TOKEN_KEY);
      localStorage.removeItem(SESSION_ID_KEY);
      setIsLoading(false);
    }
  }, []);

  return { logout, isLoading };
}
