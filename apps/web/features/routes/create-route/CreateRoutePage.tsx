'use client';

/**
 * CreateRoutePage — Plot a new community route.
 * Requirements: 1.1
 */

import { useCallback, useState } from 'react';
import Link from 'next/link';
import { CreateRouteForm } from './CreateRouteForm';
import { ThemeToggle } from '@/shared/components/ThemeToggle';

export function CreateRoutePage() {
  const [submitted, setSubmitted] = useState(false);

  const handleSuccess = useCallback(() => setSubmitted(true), []);

  if (submitted) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-indigo-50 dark:from-slate-900 dark:via-blue-950 dark:to-slate-900 flex items-center justify-center px-4">
        <div className="w-full max-w-md text-center">
          {/* Success icon */}
          <div className="mx-auto mb-6 flex h-20 w-20 items-center justify-center rounded-full bg-emerald-500/20 ring-1 ring-emerald-500/30">
            <svg className="h-10 w-10 text-emerald-500 dark:text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h2 className="text-2xl font-bold text-gray-900 dark:text-white">Route Submitted!</h2>
          <p className="mt-3 text-sm text-gray-500 dark:text-white/60">
            Your route has been saved with status <span className="font-semibold text-amber-500 dark:text-amber-400">unverified</span> and will be reviewed by the community.
          </p>
          <div className="mt-8 flex flex-col gap-3">
            <button
              onClick={() => setSubmitted(false)}
              className="min-h-[44px] w-full rounded-xl bg-blue-600 px-4 py-3 font-semibold text-white shadow-lg shadow-blue-500/30 transition hover:bg-blue-500"
            >
              Plot Another Route
            </button>
            <Link
              href="/browse"
              className="min-h-[44px] w-full rounded-xl border border-gray-200 dark:border-white/20 bg-gray-100 dark:bg-white/10 px-4 py-3 font-semibold text-gray-800 dark:text-white text-center transition hover:bg-gray-200 dark:hover:bg-white/20"
            >
              Browse Routes
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-indigo-50 dark:from-slate-900 dark:via-blue-950 dark:to-slate-900">
      {/* Header */}
      <header className="sticky top-0 z-50 border-b border-black/10 dark:border-white/10 bg-white/80 dark:bg-slate-900/80 backdrop-blur-md">
        <div className="mx-auto flex max-w-3xl items-center gap-4 px-4 py-3">
          <Link
            href="/"
            className="flex h-9 w-9 items-center justify-center rounded-xl bg-gray-100 dark:bg-white/10 text-gray-700 dark:text-white transition hover:bg-gray-200 dark:hover:bg-white/20"
            aria-label="Back to home"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
            </svg>
          </Link>
          <div className="flex-1">
            <h1 className="text-base font-bold text-gray-900 dark:text-white leading-none">Plot a Route</h1>
            <p className="mt-0.5 text-xs text-gray-500 dark:text-white/50">Contribute to the community transit map</p>
          </div>
          <ThemeToggle />
        </div>
      </header>

      {/* Body */}
      <div className="mx-auto max-w-3xl px-4 py-6">
        {/* Info banner */}
        <div className="mb-6 flex items-start gap-3 rounded-2xl border border-blue-500/20 bg-blue-500/10 px-4 py-3">
          <svg className="mt-0.5 h-4 w-4 shrink-0 text-blue-500 dark:text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <p className="text-sm text-blue-600 dark:text-blue-300">
            Tap the map to add at least <strong>2 waypoints</strong> in order. Routes are saved as <span className="text-amber-500 dark:text-amber-400 font-medium">unverified</span> until a moderator approves them.
          </p>
        </div>

        {/* Form card */}
        <div className="rounded-2xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-sm dark:shadow-none backdrop-blur-sm">
          <CreateRouteForm onSuccess={handleSuccess} />
        </div>
      </div>
    </div>
  );
}
