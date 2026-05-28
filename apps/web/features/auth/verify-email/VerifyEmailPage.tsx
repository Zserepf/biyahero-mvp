'use client';

/**
 * Email verification page — route target for /verify-email?token=xxx.
 *
 * Reads the token from URL search params and delegates to VerifyEmailForm.
 * Requirements: 5.2, 9.1
 */

import { useSearchParams, useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { VerifyEmailForm } from './VerifyEmailForm';

export function VerifyEmailPage() {
  const t = useTranslations();
  const searchParams = useSearchParams();
  const router = useRouter();
  const token = searchParams.get('token');

  function handleSuccess() {
    // After a short delay, redirect to login
    setTimeout(() => router.push('/login'), 2000);
  }

  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-8">
      <div className="w-full max-w-sm space-y-6">
        {/* Header */}
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900">{t('auth.verifyEmail')}</h1>
          <p className="mt-1 text-sm text-gray-600">{t('auth.verifyEmailSubtitle')}</p>
        </div>

        <VerifyEmailForm token={token} onSuccess={handleSuccess} />

        {/* Back to login */}
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
