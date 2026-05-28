'use client';

/**
 * Logout page — route target for /logout.
 *
 * Thin page component that composes the LogoutForm and handles
 * navigation after logout.
 */

import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { LogoutForm } from './LogoutForm';

export function LogoutPage() {
  const t = useTranslations();
  const router = useRouter();

  function handleSuccess() {
    router.push('/login');
  }

  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-8">
      <div className="w-full max-w-sm space-y-6">
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900">{t('auth.logout')}</h1>
        </div>

        <LogoutForm onSuccess={handleSuccess} />
      </div>
    </main>
  );
}
