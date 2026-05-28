'use client';

/**
 * Login page — route target for /login.
 *
 * Thin page component that composes the LoginForm and handles
 * navigation on success.
 * Requirements: 5.3, 5.6, 9.1, 9.3
 */

import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { LoginForm } from './LoginForm';

export function LoginPage() {
  const t = useTranslations();
  const router = useRouter();

  function handleSuccess() {
    router.push('/');
  }

  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-8">
      <div className="w-full max-w-sm space-y-6">
        {/* Header */}
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900">{t('auth.login')}</h1>
          <p className="mt-1 text-sm text-gray-600">{t('auth.loginSubtitle')}</p>
        </div>

        <LoginForm onSuccess={handleSuccess} />

        {/* Register link */}
        <p className="text-center text-sm text-gray-600">
          {t('auth.noAccount')}{' '}
          <Link
            href="/register"
            className="font-medium text-blue-600 hover:text-blue-500 focus:outline-none focus:underline"
          >
            {t('nav.register')}
          </Link>
        </p>
      </div>
    </main>
  );
}
