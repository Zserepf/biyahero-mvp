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
import { ThemeToggle } from '@/shared/components/ThemeToggle';
import type { AdminUserItem } from './types';

export function UserListPage() {
  const t = useTranslations();
  const { users, isLoading, error, isForbidden, refetch } = useUserList();

  const [suspendTarget, setSuspendTarget] = useState<AdminUserItem | null>(null);
  const [promoteTarget, setPromoteTarget] = useState<AdminUserItem | null>(null);

  return (
    <AuthGuard requiredRoles={['super_admin']}>
      <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-indigo-50 dark:from-slate-900 dark:via-blue-950 dark:to-slate-900">
        {/* Header */}
        <header className="sticky top-0 z-50 border-b border-black/10 dark:border-white/10 bg-white/80 dark:bg-slate-900/80 backdrop-blur-md">
          <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
            <div className="flex items-center gap-4">
              <a
                href="/"
                className="flex h-9 w-9 items-center justify-center rounded-xl bg-gray-100 dark:bg-white/10 text-gray-700 dark:text-white transition hover:bg-gray-200 dark:hover:bg-white/20"
                aria-label="Back to home"
              >
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
                </svg>
              </a>
              <div>
                <h1 className="text-base font-bold text-gray-900 dark:text-white leading-none">{t('admin.userManagement')}</h1>
                <p className="mt-0.5 text-xs text-gray-500 dark:text-white/50">Super Admin panel</p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <ThemeToggle />
              <button
                type="button"
                onClick={() => refetch()}
                disabled={isLoading}
                aria-label={t('admin.refresh')}
                className="flex items-center gap-2 rounded-xl border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5 px-4 py-2 text-sm font-medium text-gray-600 dark:text-white/70 transition hover:bg-gray-100 dark:hover:bg-white/10 hover:text-gray-900 dark:hover:text-white focus:outline-none focus:ring-2 focus:ring-blue-500/30 disabled:opacity-40"
              >
                <svg className={`h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                {t('admin.refresh')}
              </button>
            </div>
          </div>
        </header>

        <div className="mx-auto max-w-6xl px-4 py-6">
          {/* Forbidden */}
          {isForbidden && (
            <div role="alert" className="flex items-center gap-3 rounded-2xl border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-400">
              <svg className="h-4 w-4 shrink-0" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
              </svg>
              {t('admin.forbiddenMessage')}
            </div>
          )}

          {/* Error */}
          {error && !isForbidden && (
            <div role="alert" className="flex items-center gap-3 rounded-2xl border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-400">
              <svg className="h-4 w-4 shrink-0" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
              </svg>
              {error}
            </div>
          )}

          {/* Loading */}
          {isLoading && (
            <div className="flex items-center justify-center py-20" role="status" aria-label="Loading users">
              <div className="flex flex-col items-center gap-3">
                <span className="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
                <p className="text-sm text-gray-400 dark:text-white/40">Loading users…</p>
              </div>
            </div>
          )}

          {/* Table */}
          {!isLoading && !isForbidden && !error && (
            <UserListTable
              users={users}
              onSuspend={setSuspendTarget}
              onPromote={setPromoteTarget}
            />
          )}
        </div>

        {/* Dialogs */}
        {suspendTarget && (
          <SuspendUserDialog
            user={suspendTarget}
            onClose={() => setSuspendTarget(null)}
            onSuccess={() => { setSuspendTarget(null); refetch(); }}
          />
        )}
        {promoteTarget && (
          <PromoteUserModal
            user={promoteTarget}
            onClose={() => setPromoteTarget(null)}
            onSuccess={() => { setPromoteTarget(null); refetch(); }}
          />
        )}
      </div>
    </AuthGuard>
  );
}
