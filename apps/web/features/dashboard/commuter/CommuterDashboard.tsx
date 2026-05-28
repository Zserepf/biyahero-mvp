'use client';

/**
 * CommuterDashboard — Mobile-first PWA home screen for commuters.
 *
 * Layout:
 *  - Fixed full-height shell (no page scroll — the shell itself is the app)
 *  - Top bar: fixed, never scrolls
 *  - Content area: scrollable, padded so it never hides behind the bottom nav
 *  - Bottom nav: fixed to bottom, always visible
 *  - Tabs switch content in-page (no navigation for main sections)
 *  - Profile opens as a bottom sheet
 */

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMe } from '@/features/auth/useMe';
import { useLogout } from '@/features/auth/useLogout';

type Tab = 'home' | 'routes' | 'fare' | 'profile';

// ─── Home tab content ─────────────────────────────────────────────────────────

function HomeTab() {
  return (
    <div className="flex flex-col gap-4">
      {/* Primary CTA */}
      <Link
        href="/commuter/waiting"
        className="flex min-h-[80px] w-full items-center gap-4 rounded-2xl bg-gradient-to-r from-amber-500 to-orange-500 px-5 py-4 shadow-xl shadow-amber-500/20 transition active:scale-[0.98]"
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

      {/* 2-col quick actions — only unique destinations */}
      <div className="grid grid-cols-2 gap-3">
        {[
          {
            label: 'Fare Calculator',
            sub: 'LTFRB-compliant rates',
            href: '/commuter/fare',
            icon: '💰',
            bg: 'bg-emerald-500/15 border-emerald-500/20',
            text: 'text-emerald-400',
          },
          {
            label: 'Plot a Route',
            sub: 'Contribute to the map',
            href: '/commuter/routes/create',
            icon: '✏️',
            bg: 'bg-violet-500/15 border-violet-500/20',
            text: 'text-violet-400',
          },
        ].map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className={`flex flex-col gap-2 rounded-2xl border p-4 transition active:scale-95 ${item.bg}`}
          >
            <span className="text-2xl">{item.icon}</span>
            <div>
              <p className={`text-sm font-bold ${item.text}`}>{item.label}</p>
              <p className="text-xs text-white/40 mt-0.5">{item.sub}</p>
            </div>
          </Link>
        ))}
      </div>

      {/* Tip */}
      <div className="rounded-2xl border border-blue-500/20 bg-blue-500/10 px-4 py-3">
        <p className="text-xs font-semibold uppercase tracking-widest text-blue-400 mb-1">Tip</p>
        <p className="text-sm text-white/70">
          💡 Tap &quot;I&apos;m Waiting Here&quot; to signal your stop — nearby drivers will see your location on their heatmap.
        </p>
      </div>
    </div>
  );
}

// ─── Routes tab content ───────────────────────────────────────────────────────

function RoutesTab() {
  return (
    <div className="flex flex-col gap-3">
      <div>
        <h2 className="text-base font-bold text-white">Routes</h2>
        <p className="mt-0.5 text-xs text-white/50">Community-sourced transit map</p>
      </div>
      {[
        {
          label: 'Browse all routes',
          sub: 'Jeepney, UV Express, Bus, Tricycle',
          icon: '🗺️',
          href: '/commuter/routes',
        },
        {
          label: 'Plot a new route',
          sub: 'Add a missing route to the map',
          icon: '✏️',
          href: '/commuter/routes/create',
        },
      ].map((item) => (
        <Link
          key={item.href}
          href={item.href}
          className="flex min-h-[64px] items-center gap-3 rounded-2xl border border-white/10 bg-white/5 px-4 py-3 transition hover:bg-white/10 active:scale-[0.98]"
        >
          <span className="text-2xl">{item.icon}</span>
          <div className="flex-1">
            <p className="text-sm font-semibold text-white">{item.label}</p>
            <p className="text-xs text-white/40">{item.sub}</p>
          </div>
          <svg className="h-4 w-4 text-white/20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
          </svg>
        </Link>
      ))}
    </div>
  );
}

// ─── Fare tab content ─────────────────────────────────────────────────────────

function FareTab() {
  return (
    <div className="flex flex-col gap-3">
      <div>
        <h2 className="text-base font-bold text-white">Fare Calculator</h2>
        <p className="mt-0.5 text-xs text-white/50">LTFRB-compliant rates with discounts</p>
      </div>
      <div className="rounded-2xl border border-emerald-500/20 bg-emerald-500/10 px-4 py-3">
        <p className="text-xs font-semibold text-emerald-400 mb-1">What this does</p>
        <p className="text-sm text-white/70">
          Calculates the exact government-mandated fare between any two points on the map — including student, senior citizen, and PWD discounts.
        </p>
      </div>
      <Link
        href="/commuter/fare"
        className="flex min-h-[56px] items-center justify-center gap-2 rounded-2xl bg-emerald-600 px-4 py-3 font-semibold text-white shadow-lg shadow-emerald-500/20 transition hover:bg-emerald-500 active:scale-[0.98]"
      >
        <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        Open Fare Calculator
      </Link>
    </div>
  );
}

// ─── Profile tab content ──────────────────────────────────────────────────────

function ProfileTab({ onLogout }: { onLogout: () => void }) {
  const { user } = useMe();
  const initial = user?.displayName?.charAt(0).toUpperCase() ?? 'C';

  return (
    <div className="flex flex-col gap-4">
      {/* Avatar + name */}
      <div className="flex items-center gap-4 rounded-2xl border border-white/10 bg-white/5 p-4">
        <div className="flex h-14 w-14 flex-shrink-0 items-center justify-center rounded-full bg-blue-600 text-xl font-bold text-white ring-4 ring-blue-500/20">
          {initial}
        </div>
        <div className="min-w-0">
          <p className="truncate text-base font-bold text-white">{user?.displayName ?? '—'}</p>
          <p className="truncate text-sm text-white/50">{user?.email ?? '—'}</p>
          <span className="mt-1 inline-block rounded-full bg-blue-500/20 px-2.5 py-0.5 text-[11px] font-semibold text-blue-300">
            Commuter
          </span>
        </div>
      </div>

      {/* Info rows */}
      <div className="rounded-2xl border border-white/10 bg-white/5 divide-y divide-white/10">
        {[
          { label: 'Account type', value: 'Commuter' },
          { label: 'Language', value: user?.languagePreference === 'fil' ? 'Filipino' : 'English' },
        ].map((row) => (
          <div key={row.label} className="flex items-center justify-between px-4 py-3">
            <span className="text-sm text-white/50">{row.label}</span>
            <span className="text-sm font-medium text-white">{row.value}</span>
          </div>
        ))}
      </div>

      {/* Log out */}
      <button
        onClick={onLogout}
        className="flex min-h-[48px] w-full items-center justify-center gap-2 rounded-2xl border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm font-semibold text-red-400 transition hover:bg-red-500/20 active:scale-[0.98]"
      >
        <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
        </svg>
        Log Out
      </button>
    </div>
  );
}

// ─── Nav config ───────────────────────────────────────────────────────────────

const navItems: { id: Tab; label: string; icon: React.ReactNode }[] = [
  {
    id: 'home',
    label: 'Home',
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
      </svg>
    ),
  },
  {
    id: 'routes',
    label: 'Routes',
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
      </svg>
    ),
  },
  {
    id: 'fare',
    label: 'Fare',
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
      </svg>
    ),
  },
  {
    id: 'profile',
    label: 'Profile',
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
      </svg>
    ),
  },
];

// ─── Main component ───────────────────────────────────────────────────────────

export function CommuterDashboard() {
  const { user, isLoading } = useMe();
  const { logout } = useLogout();
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<Tab>('home');

  async function handleLogout() {
    await logout();
    router.push('/login');
  }

  const firstName = user?.displayName?.split(' ')[0] ?? 'there';
  const initial = user?.displayName?.charAt(0).toUpperCase() ?? 'C';
  const hour = new Date().getHours();
  const greeting = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';

  const tabTitles: Record<Tab, { title: string; sub: string }> = {
    home: { title: `${greeting}, ${firstName} 👋`, sub: "What do you need today?" },
    routes: { title: 'Routes', sub: 'Community transit map' },
    fare: { title: 'Fare Calculator', sub: 'LTFRB-compliant rates' },
    profile: { title: 'Profile', sub: 'Your account' },
  };

  return (
    // Fixed full-height shell — nothing scrolls except the content area
    <div className="fixed inset-0 flex flex-col bg-slate-950 text-white">

      {/* ── Top bar — fixed, never scrolls ───────────────────────────── */}
      <header className="flex-none border-b border-white/10 bg-slate-950 px-4 py-3">
        <div className="flex items-center justify-between">
          {/* Brand + page title */}
          <div className="flex items-center gap-3">
            <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-xl bg-blue-600">
              <svg className="h-4 w-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
            </div>
            <div>
              <p className="text-sm font-bold text-white leading-none">{tabTitles[activeTab].title}</p>
              <p className="text-[11px] text-white/40 mt-0.5">{tabTitles[activeTab].sub}</p>
            </div>
          </div>

          {/* Avatar */}
          {!isLoading && (
            <button
              onClick={() => setActiveTab(activeTab === 'profile' ? 'home' : 'profile')}
              className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-full bg-blue-600 text-sm font-bold text-white ring-2 ring-blue-500/30"
              aria-label="Open profile"
            >
              {initial}
            </button>
          )}
        </div>
      </header>

      {/* ── Scrollable content area ───────────────────────────────────── */}
      <main className="flex-1 overflow-y-auto px-4 py-4">
        {activeTab === 'home' && <HomeTab />}
        {activeTab === 'routes' && <RoutesTab />}
        {activeTab === 'fare' && <FareTab />}
        {activeTab === 'profile' && <ProfileTab onLogout={handleLogout} />}
      </main>

      {/* ── Bottom nav — fixed, always visible ───────────────────────── */}
      <nav className="flex-none border-t border-white/10 bg-slate-950">
        <div className="flex items-center justify-around px-2 py-2">
          {navItems.map((item) => {
            const isActive = activeTab === item.id;
            return (
              <button
                key={item.id}
                onClick={() => setActiveTab(item.id)}
                className={`flex min-h-[44px] min-w-[44px] flex-col items-center justify-center gap-1 rounded-xl px-3 py-2 transition ${
                  isActive ? 'text-blue-400' : 'text-white/40 hover:text-white/70'
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
