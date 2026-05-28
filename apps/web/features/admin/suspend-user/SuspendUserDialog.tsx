'use client';

/**
 * Suspend user confirmation dialog.
 *
 * Shows a confirmation prompt before suspending a user account.
 * Uses a modal overlay with accessible focus management.
 * Requirements: 5.8, 9.1, 9.4
 */

import { useEffect, useRef } from 'react';
import { useTranslations } from 'next-intl';
import { useSuspendUser } from './useSuspendUser';
import type { AdminUserItem } from '../user-list/types';

interface SuspendUserDialogProps {
  user: AdminUserItem;
  onClose: () => void;
  onSuccess: () => void;
}

export function SuspendUserDialog({ user, onClose, onSuccess }: SuspendUserDialogProps) {
  const t = useTranslations();
  const { suspendUser, isLoading, error } = useSuspendUser();
  const cancelRef = useRef<HTMLButtonElement>(null);

  // Focus the cancel button on mount for accessibility
  useEffect(() => {
    cancelRef.current?.focus();
  }, []);

  // Close on Escape key
  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape' && !isLoading) {
        onClose();
      }
    }
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [onClose, isLoading]);

  async function handleConfirm() {
    try {
      await suspendUser(user.id);
      onSuccess();
    } catch {
      // Error is displayed in the dialog
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="suspend-dialog-title"
    >
      <div className="w-full max-w-md rounded-lg bg-white dark:bg-slate-800 p-6 shadow-xl ring-1 ring-black/10 dark:ring-white/10">
        <h2
          id="suspend-dialog-title"
          className="text-lg font-semibold text-gray-900 dark:text-white"
        >
          {t('admin.suspendConfirmTitle')}
        </h2>

        <p className="mt-2 text-sm text-gray-600 dark:text-white/60">
          {t('admin.suspendConfirmMessage', { name: user.displayName, email: user.email })}
        </p>

        {/* Error */}
        {error && (
          <div role="alert" className="mt-3 rounded-md bg-red-50 dark:bg-red-500/10 p-3 text-sm text-red-700 dark:text-red-400">
            {error}
          </div>
        )}

        {/* Actions */}
        <div className="mt-6 flex items-center justify-end gap-3">
          <button
            ref={cancelRef}
            type="button"
            onClick={onClose}
            disabled={isLoading}
            className="min-h-[44px] min-w-[44px] rounded-md border border-gray-300 dark:border-white/10 px-4 py-2 text-sm font-medium text-gray-700 dark:text-white/70 hover:bg-gray-50 dark:hover:bg-white/5 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 dark:focus:ring-offset-slate-800 disabled:opacity-50"
          >
            {t('common.cancel')}
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            disabled={isLoading}
            className="min-h-[44px] min-w-[44px] rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-red-500 focus:ring-offset-2 dark:focus:ring-offset-slate-800 disabled:opacity-50"
          >
            {isLoading ? (
              <span className="inline-flex items-center gap-2">
                <span className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                {t('admin.suspending')}
              </span>
            ) : (
              t('admin.confirmSuspend')
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
