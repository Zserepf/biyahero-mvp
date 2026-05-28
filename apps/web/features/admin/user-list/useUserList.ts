'use client';

/**
 * Admin user list query hook — GET /v1/admin/users.
 *
 * Fetches all users for the Super Admin management panel.
 * Returns 403 if the current user is not a Super Admin.
 * Requirements: 5.8, 5.9
 */

import { useState, useEffect, useCallback } from 'react';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import { ApiError } from '@/shared/types/api';
import type { AdminUserItem, AdminUsersResponse } from './types';

interface UseUserListReturn {
  users: AdminUserItem[];
  isLoading: boolean;
  error: string | null;
  isForbidden: boolean;
  refetch: () => Promise<void>;
}

export function useUserList(): UseUserListReturn {
  const [users, setUsers] = useState<AdminUserItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isForbidden, setIsForbidden] = useState(false);

  const fetchUsers = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    setIsForbidden(false);

    try {
      const response = await apiClient.get<AdminUsersResponse>(
        API_ENDPOINTS.ADMIN.USERS,
      );
      setUsers(response.data);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 403) {
          setIsForbidden(true);
          setError('admin.forbidden');
        } else {
          setError(err.message);
        }
      } else {
        setError('errors.generic');
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  return { users, isLoading, error, isForbidden, refetch: fetchUsers };
}
