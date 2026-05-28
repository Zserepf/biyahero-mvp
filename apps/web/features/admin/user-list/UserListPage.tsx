'use client';

/**
 * Admin user list page — route target for the Super Admin user management panel.
 *
 * Composes the user table with suspend and promote modals.
 * Protected by AuthGuard with requiredRoles=['super_admin'].
 * Requirements: 5.8, 5.9, 5.11
 */

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { AuthGuard } from '@/features/auth/AuthGuard';
import { useUserList } from './useUserList';
import { UserListTable } from './UserListTable';
import { SuspendUserDialog } from '../suspend-user/SuspendUserDialog';
import { PromoteUserModal } from '../promote-user/PromoteUserModal';
import type { AdminUserItem } from './types';

export function UserListPage() {
  const t = useTranslations();
  const { users, isLoading, error, isForbidden, refetch } = useUserList();

  const [suspendTarget, setSuspendTarget] = useState<AdminUserItem | null>(null);
  const [promoteTarget, setPromoteTarget] = useState<AdminUserItem | null>(null);

  return (
    <AuthGuard requiredRoles={['super_admin']}>
      <div className="mx-auto max-w-6xl px-4 py-6">
        <div className="mb-6 flex items-center justify-between">
          <h1 className="text-2xl font-bold text-gray-900">
            {t('admin.userManagement')}
          </h1>
          <button
            type="button"
            onClick={() => refetch()}
            disabled={isLoading}
            aria-label={t('admin.refresh')}
            className="min-h-[44px] min-w-[44px] rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50"
          >
            {t('admin.refresh')}
          </button>
        </div>

        {/* Forbidden state */}
        {isForbidden && (
          <div role="alert" className="rounded-md bg-red-50 p-4 text-sm text-red-700">
            {t('admin.forbiddenMessage')}
          </div>
        )}

        {/* Error state */}
        {error && !isForbidden && (
          <div role="alert" className="rounded-md bg-red-50 p-4 text-sm text-red-700">
            {error}
          </div>
        )}

        {/* Loading state */}
        {isLoading && (
          <div
            className="flex items-center justify-center py-12"
            role="status"
            aria-label="Loading users"
          >
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-blue-600 border-t-transparent" />
          </div>
        )}

        {/* User table */}
        {!isLoading && !isForbidden && !error && (
          <UserListTable
            users={users}
            onSuspend={setSuspendTarget}
            onPromote={setPromoteTarget}
          />
        )}

        {/* Suspend confirmation dialog */}
        {suspendTarget && (
          <SuspendUserDialog
            user={suspendTarget}
            onClose={() => setSuspendTarget(null)}
            onSuccess={() => {
              setSuspendTarget(null);
              refetch();
            }}
          />
        )}

        {/* Promote modal with password 2FA */}
        {promoteTarget && (
          <PromoteUserModal
            user={promoteTarget}
            onClose={() => setPromoteTarget(null)}
            onSuccess={() => {
              setPromoteTarget(null);
              refetch();
            }}
          />
        )}
      </div>
    </AuthGuard>
  );
}
