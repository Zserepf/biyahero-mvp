'use client';

/**
 * Refresh page — displayed when a session has expired.
 *
 * Provides a UI for the user to attempt session renewal or redirect to login.
 */

import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { RefreshForm } from './RefreshForm';

export function RefreshPage() {
  const t = useTranslations();
  const router = useRouter();

  function handleSuccess() {
    router.back();
  }

  function handleFailure() {
    router.push('/login');
  }

  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-8">
      <div className="w-full max-w-sm space-y-6">
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900">{t('auth.sessionExpiredTitle')}</h1>
        </div>

        <RefreshForm onSuccess={handleSuccess} onFailure={handleFailure} />

        <p className="text-center text-sm text-gray-600">
          <Link
            href="/login"
            className="font-medium text-blue-600 hover:text-blue-500 focus:outline-none focus:underline"
          >
            {t('auth.backToLogin')}
          </Link>
        </p>
      </div>
    </main>
  );
}
