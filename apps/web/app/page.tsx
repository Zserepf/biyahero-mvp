'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMe } from '@/features/auth/useMe';
import { useEffect } from 'react';
import { ThemeToggle } from '@/shared/components/ThemeToggle';

// ─── Guest landing page ───────────────────────────────────────────────────────
// Authenticated users are immediately redirected to their role dashboard.

const highlights = [
  {
    icon: (
      <svg className="h-7 w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
      </svg>
    ),
    color: 'text-blue-400',
    bg: 'bg-blue-500/10 border-blue-500/20',
    title: 'Community-Sourced Routes',
    body: 'Plot, browse, and verify local jeepney, UV Express, and bus routes that mainstream maps miss.',
  },
  {
    icon: (
      <svg className="h-7 w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
      </svg>
    ),
    color: 'text-green-400',
    bg: 'bg-green-500/10 border-green-500/20',
    title: 'Anti-Scam Fare Calculator',
    body: 'Compute the exact LTFRB-compliant fare with student, senior, and PWD discounts.',
  },
  {
    icon: (
      <svg className="h-7 w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
      </svg>
    ),
    color: 'text-amber-400',
    bg: 'bg-amber-500/10 border-amber-500/20',
    title: 'Anti-123 Payment Alerts',
    body: 'Drivers get unforgeable real-time confirmation the moment a passenger pays digitally.',
  },
  {
    icon: (
      <svg className="h-7 w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
        <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
      </svg>
    ),
    color: 'text-rose-400',
    bg: 'bg-rose-500/10 border-rose-500/20',
    title: 'Real-Time Demand Heatmap',
    body: 'Commuters signal their location as live hotspots on the driver\'s map — no identity exposed.',
  },
];

function getRoleDashboard(role: string): string {
  if (role === 'driver') return '/driver/dashboard';
  if (role === 'moderator' || role === 'super_admin') return '/admin/users';
  return '/commuter/dashboard';
}

export default function Home() {
  const { user, isLoading } = useMe();
  const router = useRouter();

  // Redirect authenticated users to their role-specific dashboard
  useEffect(() => {
    if (!isLoading && user) {
      router.replace(getRoleDashboard(user.role));
    }
  }, [user, isLoading, router]);

  // Show nothing while checking auth (avoids flash of landing page for logged-in users)
  if (isLoading || user) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-900">
        <span className="h-8 w-8 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
      </div>
    );
  }

  // ── Guest landing page ──────────────────────────────────────────────────────
  return (
    <div className="min-h-screen bg-slate-900 text-white">
      {/* Header */}
      <header className="sticky top-0 z-50 border-b border-white/10 bg-slate-900/80 backdrop-blur-md">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <div className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-blue-600">
              <svg className="h-4 w-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
            </div>
            <span className="font-bold text-white">BiyaHero</span>
          </div>
          <div className="flex items-center gap-2">
            <ThemeToggle />
            <Link href="/login" className="rounded-lg px-4 py-2 text-sm font-medium text-white/70 transition hover:text-white">
              Log In
            </Link>
            <Link href="/register" className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white shadow-lg shadow-blue-500/30 transition hover:bg-blue-500">
              Sign Up
            </Link>
          </div>
        </div>
      </header>

      {/* Hero */}
      <section className="mx-auto max-w-5xl px-4 pb-12 pt-16 text-center">
        <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-blue-500/30 bg-blue-500/10 px-4 py-1.5 text-sm text-blue-300">
          <span className="h-2 w-2 animate-pulse rounded-full bg-blue-400" />
          Philippine Transit Network
        </div>
        <h1 className="text-4xl font-bold tracking-tight md:text-5xl">
          Navigate Philippine Transit
          <br />
          <span className="bg-gradient-to-r from-blue-400 to-cyan-400 bg-clip-text text-transparent">
            Like a Hero
          </span>
        </h1>
        <p className="mx-auto mt-4 max-w-xl text-base text-white/60">
          Community-sourced routes, LTFRB fare calculator, real-time demand heatmaps,
          and instant payment notifications — all in one offline-ready PWA.
        </p>
        <div className="mt-8 flex flex-wrap justify-center gap-3">
          <Link href="/register" className="min-h-[44px] rounded-xl bg-blue-600 px-6 py-3 font-semibold text-white shadow-lg shadow-blue-500/30 transition hover:bg-blue-500">
            Get Started — It&apos;s Free
          </Link>
          <Link href="/commuter/fare" className="min-h-[44px] rounded-xl border border-white/20 bg-white/10 px-6 py-3 font-semibold text-white transition hover:bg-white/20">
            Try Fare Calculator
          </Link>
        </div>
      </section>

      {/* Feature highlights */}
      <section className="mx-auto max-w-5xl px-4 pb-16">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {highlights.map((h) => (
            <div key={h.title} className={`rounded-2xl border p-5 ${h.bg}`}>
              <div className={`mb-3 ${h.color}`}>{h.icon}</div>
              <h3 className="font-semibold text-white">{h.title}</h3>
              <p className="mt-1.5 text-sm leading-relaxed text-white/60">{h.body}</p>
            </div>
          ))}
        </div>
      </section>

      {/* CTA */}
      <section className="mx-auto max-w-5xl px-4 pb-20">
        <div className="rounded-2xl border border-blue-500/30 bg-gradient-to-r from-blue-600/20 to-cyan-600/20 p-8 text-center">
          <h2 className="text-xl font-bold text-white">Ready to ride smarter?</h2>
          <p className="mx-auto mt-2 max-w-md text-sm text-white/60">
            Create a free account to signal your location to drivers, plot routes, and get real-time payment confirmations.
          </p>
          <Link href="/register" className="mt-6 inline-flex min-h-[44px] items-center rounded-xl bg-blue-600 px-6 py-3 font-semibold text-white shadow-lg shadow-blue-500/30 transition hover:bg-blue-500">
            Create Free Account
          </Link>
        </div>
      </section>

      <footer className="border-t border-white/10 py-8 text-center text-sm text-white/30">
        <p>BiyaHero MVP — Built for Filipino commuters and drivers 🇵🇭</p>
        <p className="mt-1">Offline-ready • Bilingual (EN/FIL) • WCAG 2.1 AA</p>
      </footer>
    </div>
  );
}
