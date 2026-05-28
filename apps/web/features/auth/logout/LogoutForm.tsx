'use client';

/**
 * Logout form component.
 *
 * A simple confirmation button that triggers the logout flow.
 * Clears tokens and redirects on completion.
 */

import { useTranslations } from 'next-intl';
import { useLogout } from './useLogout';

interface LogoutFormProps {
  onSuccess?: () => void;
}

export function LogoutForm({ onSuccess }: LogoutFormProps) {
  const t = useTranslations();
  const { logout, isLoading } = useLogout();

  async function handleLogout() {
    await logout();
    onSuccess?.();
  }

  return (
    <div className="space-y-4 text-center">
      <p className="text-sm text-gray-600">{t('auth.logoutConfirmation')}</p>

      <button
        type="button"
        onClick={handleLogout}
        disabled={isLoading}
        className="inline-flex items-center justify-center rounded-md bg-red-600 px-4 py-2.5 text-base font-medium text-white shadow-sm hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-red-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
      >
        {isLoading ? (
          <span className="h-5 w-5 animate-spin rounded-full border-2 border-white border-t-transparent" />
        ) : (
          t('auth.logout')
        )}
      </button>
    </div>
  );
}
