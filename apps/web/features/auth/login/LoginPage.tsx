'use client';

/**
 * Login page — route target for /login.
 * Requirements: 5.3, 5.6, 9.1, 9.3
 */

import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { LoginForm } from './LoginForm';
import { ThemeToggle } from '@/shared/components/ThemeToggle';

export function LoginPage() {
  const t = useTranslations();
  const router = useRouter();

  function handleSuccess() {
    router.push('/');
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-gradient-to-br from-blue-50 via-white to-indigo-50 dark:from-slate-900 dark:via-blue-950 dark:to-slate-900 px-4 py-8">
      <div className="w-full max-w-md">
        {/* Theme toggle */}
        <div className="mb-4 flex justify-end">
          <ThemeToggle />
        </div>
        {/* Logo / Brand */}
        <div className="mb-8 text-center">
          <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-blue-600 shadow-lg">
            <svg className="h-8 w-8 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
          <h1 className="text-3xl font-bold tracking-tight text-gray-900 dark:text-white">BiyaHero</h1>
          <p className="mt-1 text-sm text-gray-500 dark:text-white/50">{t('auth.loginSubtitle')}</p>
        </div>

        {/* Card */}
        <div className="rounded-2xl bg-white dark:bg-white/5 px-8 py-8 shadow-xl dark:shadow-none ring-1 ring-gray-100 dark:ring-white/10 backdrop-blur-sm">
          <h2 className="mb-6 text-xl font-semibold text-gray-800 dark:text-white">{t('auth.login')}</h2>
          <LoginForm onSuccess={handleSuccess} />
        </div>

        {/* Register link */}
        <p className="mt-6 text-center text-sm text-gray-500 dark:text-white/50">
          {t('auth.noAccount')}{' '}
          <Link
            href="/register"
            className="font-semibold text-blue-600 dark:text-blue-400 hover:text-blue-500 dark:hover:text-blue-300 focus:outline-none focus:underline"
          >
            {t('nav.register')}
          </Link>
        </p>
      </div>
    </main>
  );
}
