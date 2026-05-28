'use client';

/**
 * Refresh form component.
 *
 * Token refresh is typically handled automatically by the API interceptor.
 * This component provides a manual "session expired" UI with a retry button.
 */

import { useTranslations } from 'next-intl';
import { useRefresh } from './useRefresh';

interface RefreshFormProps {
  onSuccess?: () => void;
  onFailure?: () => void;
}

export function RefreshForm({ onSuccess, onFailure }: RefreshFormProps) {
  const t = useTranslations();
  const { refresh, isLoading, error } = useRefresh();

  async function handleRefresh() {
    try {
      await refresh();
      onSuccess?.();
    } catch {
      onFailure?.();
    }
  }

  return (
    <div className="space-y-4 text-center">
      {error && (
        <div role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">
          {error}
        </div>
      )}

      <p className="text-sm text-gray-600">{t('auth.sessionExpired')}</p>

      <button
        type="button"
        onClick={handleRefresh}
        disabled={isLoading}
        className="inline-flex items-center justify-center rounded-md bg-blue-600 px-4 py-2.5 text-base font-medium text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
      >
        {isLoading ? (
          <span className="h-5 w-5 animate-spin rounded-full border-2 border-white border-t-transparent" />
        ) : (
          t('auth.refreshSession')
        )}
      </button>
    </div>
  );
}
