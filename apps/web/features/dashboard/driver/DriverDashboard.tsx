'use client';

/**
 * DriverDashboard — Mobile-first PWA home screen for drivers.
 *
 * Layout:
 *  - Map fills 100% of the screen and is always interactive
 *  - Floating top bar (pointer-events only on its own elements)
 *  - Bottom nav switches between in-page panels (no navigation)
 *  - Panels slide up as a bottom sheet over the map
 */

import { useState } from 'react';
import dynamic from 'next/dynamic';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMe } from '@/features/auth/useMe';
import { useLogout } from '@/features/auth/useLogout';

const DriverHeatmapMap = dynamic(
  () => import('./DriverHeatmapMap').then((m) => m.DriverHeatmapMap),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-full w-full items-center justify-center bg-slate-900">
        <span className="h-8 w-8 animate-spin rounded-full border-2 border-purple-500 border-t-transparent" />
      </div>
    ),
  },
);

type Tab = 'map' | 'payments' | 'routes' | 'profile';

// ─── Payments panel ───────────────────────────────────────────────────────────

function PaymentsPanel() {
  return (
    <div className="flex flex-col gap-4 px-4 py-5">
      <div>
        <h2 className="text-base font-bold text-white">Payment Alerts</h2>
        <p className="mt-0.5 text-xs text-white/50">
          Real-time GCash / Maya confirmation — no more 123 scam
        </p>
      </div>

      {/* How it works */}
      <div className="rounded-2xl border border-purple-500/20 bg-purple-500/10 px-4 py-3">
        <p className="text-xs font-semibold text-purple-300 mb-2">How it works</p>
        <ol className="flex flex-col gap-1.5 text-sm text-white/70">
          <li className="flex items-start gap-2">
            <span className="mt-0.5 flex h-4 w-4 flex-shrink-0 items-center justify-center rounded-full bg-purple-500/30 text-[10px] font-bold text-purple-300">1</span>
            Passenger pays via GCash or Maya
          </li>
          <li className="flex items-start gap-2">
            <span className="mt-0.5 flex h-4 w-4 flex-shrink-0 items-center justify-center rounded-full bg-purple-500/30 text-[10px] font-bold text-purple-300">2</span>
            Server verifies the transaction instantly
          </li>
          <li className="flex items-start gap-2">
            <span className="mt-0.5 flex h-4 w-4 flex-shrink-0 items-center justify-center rounded-full bg-purple-500/30 text-[10px] font-bold text-purple-300">3</span>
            You receive an audio + visual confirmation here
          </li>
        </ol>
      </div>

      {/* Placeholder — no recent payments */}
      <div className="flex flex-col items-center justify-center rounded-2xl border border-white/10 bg-white/5 py-10 text-center">
        <svg className="h-10 w-10 text-white/20 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
        </svg>
        <p className="text-sm font-medium text-white/40">No payments yet today</p>
        <p className="mt-1 text-xs text-white/25">Confirmations will appear here in real time</p>
      </div>
    </div>
  );
}

// ─── Routes panel ─────────────────────────────────────────────────────────────

function RoutesPanel() {
  return (
    <div className="flex flex-col gap-4 px-4 py-5">
      <div>
        <h2 className="text-base font-bold text-white">Routes</h2>
        <p className="mt-0.5 text-xs text-white/50">Browse and contribute community transit routes</p>
      </div>
      <div className="flex flex-col gap-2">
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
            className="flex min-h-[60px] items-center gap-3 rounded-2xl border border-white/10 bg-white/5 px-4 py-3 transition hover:bg-white/10 active:scale-[0.98]"
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
    </div>
  );
}

// ─── Profile panel ────────────────────────────────────────────────────────────

function ProfilePanel({ onLogout }: { onLogout: () => void }) {
  const { user } = useMe();
  const initial = user?.displayName?.charAt(0).toUpperCase() ?? 'D';

  return (
    <div className="flex flex-col gap-4 px-4 py-5">
      {/* Avatar + name */}
      <div className="flex items-center gap-4">
        <div className="flex h-14 w-14 flex-shrink-0 items-center justify-center rounded-full bg-purple-600 text-xl font-bold text-white ring-4 ring-purple-500/20">
          {initial}
        </div>
        <div>
          <p className="text-base font-bold text-white">{user?.displayName ?? '—'}</p>
          <p className="text-sm text-white/50">{user?.email ?? '—'}</p>
          <span className="mt-1 inline-block rounded-full bg-purple-500/20 px-2.5 py-0.5 text-[11px] font-semibold text-purple-300">
            Driver
          </span>
        </div>
      </div>

      {/* Info rows */}
      <div className="rounded-2xl border border-white/10 bg-white/5 divide-y divide-white/10">
        {[
          { label: 'Account type', value: 'Driver' },
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
    id: 'map',
    label: 'Heatmap',
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
      </svg>
    ),
  },
  {
    id: 'payments',
    label: 'Payments',
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
      </svg>
    ),
  },
  {
    id: 'routes',
    label: 'Routes',
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
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

export function DriverDashboard() {
  const { user, isLoading } = useMe();
  const { logout } = useLogout();
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<Tab>('map');

  async function handleLogout() {
    await logout();
    router.push('/login');
  }

  const initial = user?.displayName?.charAt(0).toUpperCase() ?? 'D';
  const showPanel = activeTab !== 'map';

  return (
    // position:relative container — map is absolute underneath, overlays are absolute on top
    <div className="relative h-screen w-full overflow-hidden bg-slate-900">

      {/* ── Map — absolute, fills everything, always interactive ─────── */}
      <div className="absolute inset-0 z-0">
        <DriverHeatmapMap />
      </div>

      {/* ── Floating top bar — pointer-events only on its own children ── */}
      <div className="pointer-events-none absolute left-0 right-0 top-0 z-20 px-4 pt-4">
        <div className="pointer-events-auto flex items-center justify-between">
          {/* Status pill */}
          <div className="flex items-center gap-2.5 rounded-2xl bg-slate-900/85 px-3.5 py-2.5 shadow-xl backdrop-blur-md ring-1 ring-white/10">
            <div className="flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-lg bg-purple-600">
              <svg className="h-3.5 w-3.5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
              </svg>
            </div>
            <div>
              <p className="text-xs font-bold text-white leading-none">Driver Mode</p>
              <p className="text-[10px] text-white/50 mt-0.5">Demand heatmap live</p>
            </div>
          </div>

          {/* Avatar — tapping opens profile tab */}
          {!isLoading && (
            <button
              onClick={() => setActiveTab(activeTab === 'profile' ? 'map' : 'profile')}
              className="flex h-10 w-10 items-center justify-center rounded-full bg-purple-600 text-sm font-bold text-white shadow-xl ring-2 ring-purple-500/40"
              aria-label="Open profile"
            >
              {initial}
            </button>
          )}
        </div>
      </div>

      {/* ── Bottom sheet panel (Payments / Routes / Profile) ─────────── */}
      {showPanel && (
        <div className="absolute bottom-[64px] left-0 right-0 z-20 max-h-[55vh] overflow-y-auto rounded-t-3xl border-t border-white/10 bg-slate-900/95 shadow-2xl backdrop-blur-md">
          {/* Drag handle */}
          <div className="flex justify-center pt-3 pb-1">
            <div className="h-1 w-10 rounded-full bg-white/20" />
          </div>

          {activeTab === 'payments' && <PaymentsPanel />}
          {activeTab === 'routes' && <RoutesPanel />}
          {activeTab === 'profile' && <ProfilePanel onLogout={handleLogout} />}
        </div>
      )}

      {/* ── Bottom nav ───────────────────────────────────────────────── */}
      <nav className="absolute bottom-0 left-0 right-0 z-20 border-t border-white/10 bg-slate-900/95 backdrop-blur-md">
        <div className="flex items-center justify-around px-2 py-2">
          {navItems.map((item) => {
            const isActive = activeTab === item.id;
            return (
              <button
                key={item.id}
                onClick={() => setActiveTab(isActive && item.id !== 'map' ? 'map' : item.id)}
                className={`flex min-h-[44px] min-w-[44px] flex-col items-center justify-center gap-1 rounded-xl px-3 py-2 transition ${
                  isActive ? 'text-purple-400' : 'text-white/40 hover:text-white/70'
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
