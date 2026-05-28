'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMe } from '@/features/auth/useMe';
import { useLogout } from '@/features/auth/useLogout';
import { useState } from 'react';
import { ThemeToggle } from '@/shared/components/ThemeToggle';

const features = [
  {
    title: 'Fare Calculator',
    description: 'Calculate LTFRB-compliant fares with distance, vehicle type, and discount support.',
    href: '/commuter/fare',
    icon: (
      <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
      </svg>
    ),
    gradient: 'from-green-500 to-emerald-600',
    roles: ['commuter', 'driver', 'moderator', 'super_admin', null],
  },
  {
    title: 'Browse Routes',
    description: 'Explore community-sourced jeepney, bus, UV Express, and tricycle routes.',
    href: '/commuter/routes',
    icon: (
      <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
      </svg>
    ),
    gradient: 'from-blue-500 to-indigo-600',
    roles: ['commuter', 'driver', 'moderator', 'super_admin', null],
  },
  {
    title: "I'm Waiting Here",
    description: 'Signal your location to nearby drivers. Help them find you in real time.',
    href: '/commuter/waiting',
    icon: (
      <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
        <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
      </svg>
    ),
    gradient: 'from-amber-500 to-orange-600',
    roles: ['commuter', 'moderator', 'super_admin'],
  },
  {
    title: 'Driver Dashboard',
    description: 'See real-time demand heatmap and receive instant payment confirmations.',
    href: '/driver/heatmap',
    icon: (
      <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
      </svg>
    ),
    gradient: 'from-purple-500 to-violet-600',
    roles: ['driver', 'moderator', 'super_admin'],
  },
  {
    title: 'Plot a Route',
    description: 'Contribute a new transit route with waypoints plotted on the map.',
    href: '/commuter/routes/create',
    icon: (
      <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
      </svg>
    ),
    gradient: 'from-teal-500 to-cyan-600',
    roles: ['commuter', 'driver', 'moderator', 'super_admin'],
  },
  {
    title: 'Admin Panel',
    description: 'Manage users, moderate routes, and monitor system health.',
    href: '/admin/users',
    icon: (
      <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
        <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
      </svg>
    ),
    gradient: 'from-gray-500 to-slate-600',
    roles: ['moderator', 'super_admin'],
  },
];

const roleColors: Record<string, string> = {
  commuter: 'bg-blue-100 text-blue-700',
  driver: 'bg-purple-100 text-purple-700',
  moderator: 'bg-amber-100 text-amber-700',
  super_admin: 'bg-red-100 text-red-700',
};

const roleLabels: Record<string, string> = {
  commuter: 'Commuter',
  driver: 'Driver',
  moderator: 'Moderator',
  super_admin: 'Super Admin',
};

// Feature highlights shown only to guests on the landing page
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
    body: 'Plot, browse, and verify local jeepney, UV Express, and bus routes that mainstream maps miss. Every route is community-edited and moderator-verified.',
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
    body: 'Compute the exact LTFRB-compliant fare between any two points — with student, senior, and PWD discounts. Know the correct fare before you board.',
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
    body: 'Drivers receive an unforgeable real-time audio and visual confirmation the moment a passenger pays via digital wallet — no more "123 scam" disputes.',
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
    body: 'Commuters signal "I\'m waiting here" and appear as live hotspots on the driver\'s map — without exposing personal identity. Drivers reach passengers faster.',
  },
];

export default function Home() {
  const { user, isLoading } = useMe();
  const { logout } = useLogout();
  const router = useRouter();
  const [menuOpen, setMenuOpen] = useState(false);

  async function handleLogout() {
    await logout();
    router.push('/login');
  }

  const visibleFeatures = features.filter((f) => {
    if (!user) return f.roles.includes(null);
    return f.roles.includes(user.role);
  });

  const isGuest = !isLoading && !user;

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-blue-950 to-slate-900 dark:from-slate-900 dark:via-blue-950 dark:to-slate-900 from-blue-50 via-white to-indigo-50">
      {/* ── Header ─────────────────────────────────────────────────────── */}
      <header className="sticky top-0 z-50 border-b border-black/10 bg-white/80 dark:border-white/10 dark:bg-slate-900/80 backdrop-blur-md">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
          <div className="flex items-center gap-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-blue-600 shadow-lg shadow-blue-500/30">
              <svg className="h-5 w-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
            </div>
            <span className="text-lg font-bold text-gray-900 dark:text-white">BiyaHero</span>
          </div>

          <div className="flex items-center gap-3">
            <ThemeToggle />
            {isLoading ? (
              <div className="h-8 w-24 animate-pulse rounded-lg bg-gray-200 dark:bg-white/10" />
            ) : user ? (
              <div className="relative">
                <button
                  onClick={() => setMenuOpen((v) => !v)}
                  className="flex items-center gap-2 rounded-xl bg-gray-100 dark:bg-white/10 px-3 py-2 text-sm text-gray-800 dark:text-white transition hover:bg-gray-200 dark:hover:bg-white/20"
                >
                  <div className="flex h-7 w-7 items-center justify-center rounded-full bg-blue-500 text-xs font-bold text-white">
                    {user.displayName.charAt(0).toUpperCase()}
                  </div>
                  <div className="text-left">
                    <p className="text-sm font-medium leading-none">{user.displayName}</p>
                    <span className={`mt-0.5 inline-block rounded-full px-1.5 py-0.5 text-[10px] font-semibold ${roleColors[user.role] ?? 'bg-gray-100 text-gray-700'}`}>
                      {roleLabels[user.role] ?? user.role}
                    </span>
                  </div>
                  <svg className="h-4 w-4 text-gray-400 dark:text-white/60" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
                  </svg>
                </button>
                {menuOpen && (
                  <div className="absolute right-0 mt-2 w-48 rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-slate-800 py-1 shadow-xl">
                    <div className="border-b border-gray-100 dark:border-white/10 px-4 py-2">
                      <p className="text-xs text-gray-400 dark:text-white/50">Signed in as</p>
                      <p className="truncate text-sm font-medium text-gray-900 dark:text-white">{user.email}</p>
                    </div>
                    <button
                      onClick={handleLogout}
                      className="flex w-full items-center gap-2 px-4 py-2 text-sm text-red-500 hover:bg-gray-50 dark:hover:bg-white/5"
                    >
                      <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                      </svg>
                      Log Out
                    </button>
                  </div>
                )}
              </div>
            ) : (
              <div className="flex items-center gap-2">
                <Link href="/login" className="rounded-lg px-4 py-2 text-sm font-medium text-gray-600 dark:text-white/80 transition hover:text-gray-900 dark:hover:text-white">
                  Log In
                </Link>
                <Link href="/register" className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white shadow-lg shadow-blue-500/30 transition hover:bg-blue-500">
                  Sign Up
                </Link>
              </div>
            )}
          </div>
        </div>
      </header>

      {/* ── Hero ───────────────────────────────────────────────────────── */}
      <section className="mx-auto max-w-6xl px-4 pb-12 pt-16 text-center">
        <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-blue-500/30 bg-blue-500/10 px-4 py-1.5 text-sm text-blue-600 dark:text-blue-300">
          <span className="h-2 w-2 rounded-full bg-blue-500 dark:bg-blue-400 animate-pulse" />
          Philippine Transit Network
        </div>
        <h1 className="text-4xl font-bold tracking-tight text-gray-900 dark:text-white md:text-5xl">
          Navigate Philippine Transit
          <br />
          <span className="bg-gradient-to-r from-blue-500 to-cyan-500 dark:from-blue-400 dark:to-cyan-400 bg-clip-text text-transparent">
            Like a Hero
          </span>
        </h1>
        <p className="mx-auto mt-4 max-w-2xl text-lg text-gray-600 dark:text-white/60">
          Community-sourced routes, LTFRB fare calculator, real-time demand heatmaps,
          and instant payment notifications — all in one offline-ready PWA.
        </p>
        {isGuest && (
          <div className="mt-8 flex flex-wrap justify-center gap-4">
            <Link
              href="/register"
              className="min-h-[44px] rounded-xl bg-blue-600 px-6 py-3 font-semibold text-white shadow-lg shadow-blue-500/30 transition hover:bg-blue-500"
            >
              Get Started — It&apos;s Free
            </Link>
            <Link
              href="/login"
              className="min-h-[44px] rounded-xl border border-gray-300 dark:border-white/20 bg-gray-100 dark:bg-white/10 px-6 py-3 font-semibold text-gray-800 dark:text-white backdrop-blur-sm transition hover:bg-gray-200 dark:hover:bg-white/20"
            >
              Log In
            </Link>
          </div>
        )}
      </section>

      {/* ── Guest feature cards (centered 2-col) ───────────────────── */}
      {isGuest && (
        <section className="mx-auto max-w-6xl px-4 pb-14">
          <p className="mb-6 text-center text-sm font-medium text-gray-400 dark:text-white/40 uppercase tracking-widest">
            Available without an account
          </p>
          <div className="flex flex-wrap justify-center gap-4">
            {visibleFeatures.map((feature) => (
              <Link
                key={feature.href}
                href={feature.href}
                className="group relative w-full max-w-sm overflow-hidden rounded-2xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-sm dark:shadow-none backdrop-blur-sm transition-all hover:border-gray-300 dark:hover:border-white/20 hover:shadow-md dark:hover:bg-white/10"
              >
                <div className={`mb-4 inline-flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br ${feature.gradient} text-white shadow-lg`}>
                  {feature.icon}
                </div>
                <h3 className="text-lg font-semibold text-gray-900 dark:text-white">{feature.title}</h3>
                <p className="mt-2 text-sm text-gray-500 dark:text-white/60">{feature.description}</p>
                <div className="mt-4 flex items-center gap-1 text-xs font-medium text-gray-400 dark:text-white/40 transition group-hover:text-gray-600 dark:group-hover:text-white/70">
                  Open
                  <svg className="h-3.5 w-3.5 transition group-hover:translate-x-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
                  </svg>
                </div>
              </Link>
            ))}
          </div>
        </section>
      )}

      {/* ── Feature highlights (guests only) ───────────────────────── */}
      {isGuest && (
        <section className="mx-auto max-w-6xl px-4 pb-16">
          <div className="mb-10 text-center">
            <h2 className="text-2xl font-bold text-gray-900 dark:text-white md:text-3xl">
              Built for Filipino commuters and drivers
            </h2>
            <p className="mx-auto mt-3 max-w-xl text-sm text-gray-500 dark:text-white/50">
              Every feature targets a real problem on Philippine roads — from overcharging to payment scams to invisible demand.
            </p>
          </div>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {highlights.map((h) => (
              <div
                key={h.title}
                className={`rounded-2xl border p-6 backdrop-blur-sm ${h.bg}`}
              >
                <div className={`mb-3 ${h.color}`}>{h.icon}</div>
                <h3 className="text-base font-semibold text-gray-900 dark:text-white">{h.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-gray-600 dark:text-white/60">{h.body}</p>
              </div>
            ))}
          </div>
        </section>
      )}

      {/* ── CTA banner (guests only) ────────────────────────────────── */}
      {isGuest && (
        <section className="mx-auto max-w-6xl px-4 pb-20">
          <div className="rounded-2xl border border-blue-500/30 bg-gradient-to-r from-blue-600/20 to-cyan-600/20 p-8 text-center backdrop-blur-sm">
            <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
              Ready to ride smarter?
            </h2>
            <p className="mx-auto mt-2 max-w-md text-sm text-gray-600 dark:text-white/60">
              Create a free account to signal your location to drivers, plot routes, and get real-time payment confirmations.
            </p>
            <div className="mt-6 flex flex-wrap justify-center gap-3">
              <Link
                href="/register"
                className="min-h-[44px] rounded-xl bg-blue-600 px-6 py-3 font-semibold text-white shadow-lg shadow-blue-500/30 transition hover:bg-blue-500"
              >
                Create Free Account
              </Link>
              <Link
                href="/commuter/fare"
                className="min-h-[44px] rounded-xl border border-gray-300 dark:border-white/20 bg-gray-100 dark:bg-white/10 px-6 py-3 font-semibold text-gray-800 dark:text-white transition hover:bg-gray-200 dark:hover:bg-white/20"
              >
                Try Fare Calculator
              </Link>
            </div>
          </div>
        </section>
      )}

      {/* ── Authenticated feature grid ──────────────────────────────── */}
      {!isGuest && (
        <section className="mx-auto max-w-6xl px-4 pb-20">
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
            {visibleFeatures.map((feature) => (
              <Link
                key={feature.href}
                href={feature.href}
                className="group relative overflow-hidden rounded-2xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-sm dark:shadow-none backdrop-blur-sm transition-all hover:border-gray-300 dark:hover:border-white/20 hover:shadow-md dark:hover:bg-white/10"
              >
                <div className={`mb-4 inline-flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br ${feature.gradient} text-white shadow-lg`}>
                  {feature.icon}
                </div>
                <h3 className="text-lg font-semibold text-gray-900 dark:text-white">{feature.title}</h3>
                <p className="mt-2 text-sm text-gray-500 dark:text-white/60">{feature.description}</p>
                <div className="mt-4 flex items-center gap-1 text-xs font-medium text-gray-400 dark:text-white/40 transition group-hover:text-gray-600 dark:group-hover:text-white/70">
                  Open
                  <svg className="h-3.5 w-3.5 transition group-hover:translate-x-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
                  </svg>
                </div>
              </Link>
            ))}
          </div>
        </section>
      )}

      {/* ── Footer ─────────────────────────────────────────────────────── */}
      <footer className="border-t border-gray-200 dark:border-white/10 py-8">
        <div className="mx-auto max-w-6xl px-4 text-center text-sm text-gray-400 dark:text-white/30">
          <p>BiyaHero MVP — Built for Filipino commuters and drivers 🇵🇭</p>
          <p className="mt-1">Offline-ready • Bilingual (EN/FIL) • WCAG 2.1 AA</p>
        </div>
      </footer>
    </div>
  );
}
