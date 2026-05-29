'use client';

/**
 * Registration page — route target for /register.
 * Requirements: 5.1, 9.1, 9.3
 */

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { RegisterForm } from './RegisterForm';
import { ThemeToggle } from '@/shared/components/ThemeToggle';
import type { RegisterResponse } from './types';

export function RegisterPage() {
  const t = useTranslations();
  const router = useRouter();
  const [registered, setRegistered] = useState(false);
  const [registeredEmail, setRegisteredEmail] = useState('');

  function handleSuccess(response: RegisterResponse) {
    setRegisteredEmail(response.email);
    setRegistered(true);
  }

  if (registered) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-gradient-to-br from-blue-50 via-white to-indigo-50 dark:from-slate-900 dark:via-blue-950 dark:to-slate-900 px-4 py-8">
        <div className="w-full max-w-md text-center">
          <div className="mx-auto mb-6 flex h-20 w-20 items-center justify-center rounded-full bg-emerald-500/20 ring-1 ring-emerald-500/30">
            <svg className="h-10 w-10 text-emerald-500 dark:text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h2 className="text-2xl font-bold text-gray-900 dark:text-white">Account Created!</h2>
          <p className="mt-3 text-sm text-gray-500 dark:text-white/60">
            Your account for{' '}
            <span className="font-semibold text-gray-900 dark:text-white">{registeredEmail}</span>{' '}
            is ready. You can log in now.
          </p>
          <button
            onClick={() => router.push('/login')}
            className="mt-8 min-h-[44px] w-full rounded-xl bg-blue-600 px-4 py-3 font-semibold text-white shadow-lg shadow-blue-500/30 transition hover:bg-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/50"
          >
            Go to Log In
          </button>
        </div>
      </main>
    );
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-gradient-to-br from-blue-50 via-white to-indigo-50 dark:from-slate-900 dark:via-blue-950 dark:to-slate-900 px-4 py-8">
      <div className="w-full max-w-md">
        {/* Theme toggle */}
        <div className="mb-4 flex justify-end">
          <ThemeToggle />
        </div>
        {/* Brand */}
        <div className="mb-8 text-center">
          <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-blue-600 shadow-lg shadow-blue-500/30">
            <svg className="h-8 w-8 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
          <h1 className="text-3xl font-bold tracking-tight text-gray-900 dark:text-white">BiyaHero</h1>
          <p className="mt-1 text-sm text-gray-500 dark:text-white/50">{t('auth.registerSubtitle')}</p>
        </div>

        {/* Card */}
        <div className="rounded-2xl border border-gray-100 dark:border-white/10 bg-white dark:bg-white/5 px-8 py-8 shadow-xl dark:shadow-none backdrop-blur-sm">
          <h2 className="mb-6 text-xl font-semibold text-gray-800 dark:text-white">{t('auth.register')}</h2>
          <RegisterForm onSuccess={handleSuccess} />
        </div>

        {/* Login link */}
        <p className="mt-6 text-center text-sm text-gray-500 dark:text-white/50">
          {t('auth.hasAccount')}{' '}
          <Link
            href="/login"
            className="font-semibold text-blue-600 dark:text-blue-400 transition hover:text-blue-500 dark:hover:text-blue-300 focus:outline-none focus:underline"
          >
            {t('auth.login')}
          </Link>
        </p>
      </div>
    </main>
  );
}
