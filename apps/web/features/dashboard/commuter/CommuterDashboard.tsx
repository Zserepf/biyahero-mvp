'use client';

/**
 * CommuterDashboard — Mobile-first PWA home screen for commuters.
 *
 * Layout: sticky top bar → greeting + quick actions → bottom nav
 * Feels like a native transit app, not a website.
 */

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMe } from '@/features/auth/useMe';
import { useLogout } from '@/features/auth/useLogout';

// ─── Quick action cards ───────────────────────────────────────────────────────

const quickActions = [
  {
    id: 'waiting',
    label: "I'm Waiting",
    sublabel: 'Signal your stop',
    href: '/commuter/waiting',
    icon: (
      <svg className="h-7 w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
        <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
      </svg>
    ),
    bg: 'bg-amber-500',
    shadow: 'shadow-amber-500/30',
    primary: true,
  },
  {
    id: 'fare',
    label: 'Fare Check',
    sublabel: 'LTFRB rates',
    href: '/commuter/fare',
    icon: (
      <svg className="h-7 w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
      </svg>
    ),
    bg: 'bg-emerald-500',
    shadow: 'shadow-emerald-500/30',
    primary: false,
  },
  {
    id: 'routes',
    label: 'Routes',
    sublabel: 'Browse transit',
    href: '/commuter/routes',
    icon: (
      <svg className="h-7 w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
      </svg>
    ),
    bg: 'bg-blue-500',
    shadow: 'shadow-blue-500/30',
    primary: false,
  },
  {
    id: 'plot',
    label: 'Plot Route',
    sublabel: 'Contribute',
    href: '/commuter/routes/create',
    icon: (
      <svg className="h-7 w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
      </svg>
    ),
    bg: 'bg-violet-500',
    shadow: 'shadow-violet-500/30',
    primary: false,
  },
];

// ─── Bottom nav items ─────────────────────────────────────────────────────────

const navItems = [
  {
    id: 'home',
    label: 'Home',
    href: '/commuter/dashboard',
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
      </svg>
    ),
  },
  {
    id: 'routes',
    label: 'Routes',
    href: '/commuter/routes',
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
      </svg>
    ),
  },
  {
    id: 'fare',
    label: 'Fare',
    href: '/commuter/fare',
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
      </svg>
    ),
  },
  {
    id: 'profile',
    label: 'Profile',
    href: '#profile',
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
      </svg>
    ),
  },
];

// ─── Tips carousel ────────────────────────────────────────────────────────────

const tips = [
  { emoji: '💡', text: 'Tap "I\'m Waiting" to signal your stop to nearby drivers.' },
  { emoji: '🧾', text: 'Use Fare Check before boarding to know the exact LTFRB rate.' },
  { emoji: '🗺️', text: 'Browse community routes to find jeepney and UV Express lines.' },
  { emoji: '✏️', text: 'Know a route that\'s missing? Plot it and help the community.' },
];

// ─── Component ────────────────────────────────────────────────────────────────

export function CommuterDashboard() {
  const { user, isLoading } = useMe();
  const { logout } = useLogout();
  const router = useRouter();
  const [activeNav, setActiveNav] = useState('home');
  const [profileOpen, setProfileOpen] = useState(false);
  const [tipIndex] = useState(() => Math.floor(Math.random() * tips.length));

  async function handleLogout() {
    await logout();
    router.push('/login');
  }

  const firstName = user?.displayName?.split(' ')[0] ?? 'Commuter';
  const initial = user?.displayName?.charAt(0).toUpperCase() ?? 'C';

  const hour = new Date().getHours();
  const greeting = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';

  return (
    <div className="flex h-screen flex-col bg-slate-950 text-white">

      {/* ── Top bar ──────────────────────────────────────────────────────── */}
      <header className="flex-none px-4 pt-safe-top pb-2 pt-4">
        <div className="flex items-center justify-between">
          {/* Brand */}
          <div className="flex items-center gap-2">
            <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-blue-600">
              <svg className="h-4 w-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
            </div>
            <span className="text-sm font-bold text-white/90">BiyaHero</span>
          </div>

          {/* Avatar */}
          {isLoading ? (
            <div className="h-9 w-9 animate-pulse rounded-full bg-white/10" />
          ) : (
            <div className="relative">
              <button
                onClick={() => setProfileOpen((v) => !v)}
                className="flex h-9 w-9 items-center justify-center rounded-full bg-blue-600 text-sm font-bold text-white ring-2 ring-blue-500/30"
                aria-label="Open profile menu"
              >
                {initial}
              </button>
              {profileOpen && (
                <div className="absolute right-0 top-11 z-50 w-52 rounded-2xl border border-white/10 bg-slate-800 py-1 shadow-2xl">
                  <div className="border-b border-white/10 px-4 py-3">
                    <p className="text-xs text-white/40">Signed in as</p>
                    <p className="truncate text-sm font-semibold text-white">{user?.displayName}</p>
                    <p className="truncate text-xs text-white/50">{user?.email}</p>
                  </div>
                  <button
                    onClick={handleLogout}
                    className="flex w-full items-center gap-2 px-4 py-3 text-sm text-red-400 transition hover:bg-white/5"
                  >
                    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                    </svg>
                    Log Out
                  </button>
                </div>
              )}
            </div>
          )}
        </div>
      </header>

      {/* ── Scrollable body ──────────────────────────────────────────────── */}
      <main className="flex-1 overflow-y-auto px-4 pb-4">

        {/* Greeting */}
        <div className="mb-6 mt-2">
          <p className="text-sm text-white/50">{greeting},</p>
          <h1 className="text-2xl font-bold text-white">{firstName} 👋</h1>
        </div>

        {/* Primary CTA — I'm Waiting Here */}
        <Link
          href="/commuter/waiting"
          className="mb-4 flex min-h-[80px] w-full items-center gap-4 rounded-2xl bg-gradient-to-r from-amber-500 to-orange-500 px-5 py-4 shadow-xl shadow-amber-500/20 transition active:scale-[0.98]"
          aria-label="Signal that you are waiting for a ride"
        >
          <div className="flex h-12 w-12 flex-shrink-0 items-center justify-center rounded-xl bg-white/20">
            <svg className="h-6 w-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
          <div className="flex-1">
            <p className="text-base font-bold text-white">I&apos;m Waiting Here</p>
            <p className="text-sm text-white/70">Signal your stop to nearby drivers</p>
          </div>
          <svg className="h-5 w-5 text-white/60" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
          </svg>
        </Link>

        {/* Quick action grid */}
        <div className="mb-6 grid grid-cols-3 gap-3">
          {quickActions.filter(a => a.id !== 'waiting').map((action) => (
            <Link
              key={action.id}
              href={action.href}
              className="flex flex-col items-center gap-2 rounded-2xl border border-white/10 bg-white/5 px-2 py-4 transition active:scale-95 hover:bg-white/10"
            >
              <div className={`flex h-11 w-11 items-center justify-center rounded-xl ${action.bg} shadow-lg ${action.shadow}`}>
                {action.icon}
              </div>
              <span className="text-center text-xs font-semibold text-white leading-tight">{action.label}</span>
              <span className="text-center text-[10px] text-white/40 leading-tight">{action.sublabel}</span>
            </Link>
          ))}
        </div>

        {/* Tip card */}
        <div className="mb-6 rounded-2xl border border-blue-500/20 bg-blue-500/10 px-4 py-3">
          <p className="text-xs font-semibold uppercase tracking-widest text-blue-400 mb-1">Tip</p>
          <p className="text-sm text-white/70">
            {tips[tipIndex].emoji} {tips[tipIndex].text}
          </p>
        </div>

        {/* Recent / explore section */}
        <div className="mb-2">
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-widest text-white/40">Explore</h2>
          <div className="flex flex-col gap-2">
            {[
              { label: 'Browse community routes', href: '/commuter/routes', icon: '🗺️', sub: 'Jeepney, UV Express, Bus' },
              { label: 'Calculate your fare', href: '/commuter/fare', icon: '💰', sub: 'LTFRB-compliant rates' },
              { label: 'Plot a missing route', href: '/commuter/routes/create', icon: '✏️', sub: 'Help the community' },
            ].map((item) => (
              <Link
                key={item.href}
                href={item.href}
                className="flex min-h-[56px] items-center gap-3 rounded-2xl border border-white/10 bg-white/5 px-4 py-3 transition hover:bg-white/10 active:scale-[0.98]"
              >
                <span className="text-xl">{item.icon}</span>
                <div className="flex-1">
                  <p className="text-sm font-medium text-white">{item.label}</p>
                  <p className="text-xs text-white/40">{item.sub}</p>
                </div>
                <svg className="h-4 w-4 text-white/20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
                </svg>
              </Link>
            ))}
          </div>
        </div>
      </main>

      {/* ── Bottom nav ───────────────────────────────────────────────────── */}
      <nav className="flex-none border-t border-white/10 bg-slate-900/95 pb-safe-bottom backdrop-blur-md">
        <div className="flex items-center justify-around px-2 py-2">
          {navItems.map((item) => {
            const isActive = activeNav === item.id;
            const isProfileItem = item.id === 'profile';
            return (
              <button
                key={item.id}
                onClick={() => {
                  setActiveNav(item.id);
                  if (isProfileItem) {
                    setProfileOpen((v) => !v);
                  } else {
                    router.push(item.href);
                  }
                }}
                className={`flex min-h-[44px] min-w-[44px] flex-col items-center justify-center gap-1 rounded-xl px-3 py-2 transition ${
                  isActive
                    ? 'text-blue-400'
                    : 'text-white/40 hover:text-white/70'
                }`}
                aria-label={item.label}
                aria-current={isActive ? 'page' : undefined}
              >
                {item.icon}
                <span className="text-[10px] font-medium">{item.label}</span>
              </button>
            );
          })}
        </div>
      </nav>
    </div>
  );
}
