'use client';

/**
 * Login page — route target for /login.
 * Requirements: 5.3, 5.6, 9.1, 9.3
 */

import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { LoginForm } from './LoginForm';
import { ThemeToggle } from '@/shared/components/ThemeToggle';
import type { UserRole } from './types';

function getRoleRedirect(role: UserRole): string {
  if (role === 'driver') return '/driver/dashboard';
  return '/commuter/dashboard';
}

export function LoginPage() {
  const router = useRouter();

  function handleSuccess(role?: UserRole) {
    router.push(role ? getRoleRedirect(role) : '/');
  }

  return (
    <main className="flex min-h-screen">
      {/* ── Left panel — brand / hero (hidden on mobile) ─────────── */}
      <div className="hidden lg:flex lg:w-1/2 flex-col justify-between bg-gradient-to-br from-blue-700 via-blue-600 to-indigo-700 p-12 text-white">
        {/* Logo */}
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/20 backdrop-blur-sm">
            <svg className="h-6 w-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
          <span className="text-xl font-bold tracking-tight">BiyaHero</span>
        </div>

        {/* Hero copy */}
        <div className="space-y-6">
          <div className="inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-4 py-1.5 text-sm backdrop-blur-sm">
            <span className="h-2 w-2 rounded-full bg-green-400 animate-pulse" />
            Philippine Transit Network
          </div>
          <h1 className="text-4xl font-bold leading-tight tracking-tight">
            Navigate Philippine<br />Transit Like a Hero
          </h1>
          <p className="text-base leading-relaxed text-white/70">
            Community-sourced routes, LTFRB fare calculator, real-time demand heatmaps,
            and instant payment notifications — all in one offline-ready PWA.
          </p>

          {/* Feature pills */}
          <div className="flex flex-wrap gap-2 pt-2">
            {[
              '🗺️ Community Routes',
              '💰 Fare Calculator',
              '📍 Demand Heatmap',
              '🔔 Payment Alerts',
            ].map((f) => (
              <span
                key={f}
                className="rounded-full border border-white/20 bg-white/10 px-3 py-1 text-sm backdrop-blur-sm"
              >
                {f}
              </span>
            ))}
          </div>
        </div>

        {/* Footer */}
        <p className="text-sm text-white/40">
          Offline-ready · Bilingual (EN/FIL) · WCAG 2.1 AA
        </p>
      </div>

      {/* ── Right panel — login form ──────────────────────────────── */}
      <div className="flex flex-1 flex-col bg-white dark:bg-slate-900">
        {/* Top bar */}
        <div className="flex items-center justify-between px-6 py-4">
          {/* Mobile logo */}
          <div className="flex items-center gap-2 lg:hidden">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-blue-600">
              <svg className="h-4 w-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
            </div>
            <span className="font-bold text-gray-900 dark:text-white">BiyaHero</span>
          </div>
          <div className="ml-auto">
            <ThemeToggle />
          </div>
        </div>

        {/* Form area */}
        <div className="flex flex-1 items-center justify-center px-6 py-8">
          <div className="w-full max-w-sm space-y-8">
            {/* Heading */}
            <div>
              <h2 className="text-2xl font-bold tracking-tight text-gray-900 dark:text-white">
                Welcome back
              </h2>
              <p className="mt-1 text-sm text-gray-500 dark:text-white/50">
                Sign in to your BiyaHero account
              </p>
            </div>

            {/* Form */}
            <LoginForm onSuccess={handleSuccess} />

            {/* Register link */}
            <p className="text-center text-sm text-gray-500 dark:text-white/50">
              Don&apos;t have an account?{' '}
              <Link
                href="/register"
                className="font-semibold text-blue-600 dark:text-blue-400 hover:text-blue-500 dark:hover:text-blue-300 focus:outline-none focus:underline"
              >
                Sign up free
              </Link>
            </p>
          </div>
        </div>
      </div>
    </main>
  );
}
