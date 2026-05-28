'use client';

/**
 * Registration page — route target for /register.
 *
 * Thin page component that composes the RegisterForm and handles
 * navigation on success.
 * Requirements: 5.1, 9.1, 9.3
 */

import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { RegisterForm } from './RegisterForm';

export function RegisterPage() {
  const t = useTranslations();
  const router = useRouter();

  function handleSuccess() {
    // Redirect to a "check your email" confirmation or login page
    router.push('/login?registered=true');
  }

  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-8">
      <div className="w-full max-w-sm space-y-6">
        {/* Header */}
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900">{t('auth.register')}</h1>
          <p className="mt-1 text-sm text-gray-600">{t('auth.registerSubtitle')}</p>
        </div>

        <RegisterForm onSuccess={handleSuccess} />

        {/* Login link */}
        <p className="text-center text-sm text-gray-600">
          {t('auth.hasAccount')}{' '}
          <Link
            href="/login"
            className="font-medium text-blue-600 hover:text-blue-500 focus:outline-none focus:underline"
          >
            {t('auth.login')}
          </Link>
        </p>
      </div>
    </main>
  );
}
