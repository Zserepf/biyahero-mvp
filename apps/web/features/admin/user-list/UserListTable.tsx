'use client';

/**
 * Admin user list table component.
 * Requirements: 5.8, 5.9, 9.1, 9.2, 9.3
 */

import { useTranslations } from 'next-intl';
import type { AdminUserItem } from './types';

interface UserListTableProps {
  users: AdminUserItem[];
  onSuspend: (user: AdminUserItem) => void;
  onPromote: (user: AdminUserItem) => void;
}

const ROLE_LABELS: Record<string, string> = {
  commuter: 'Commuter',
  driver: 'Driver',
  moderator: 'Moderator',
  super_admin: 'Super Admin',
};

const ROLE_STYLES: Record<string, string> = {
  commuter:   'bg-blue-500/20 text-blue-400 ring-1 ring-blue-500/30',
  driver:     'bg-purple-500/20 text-purple-400 ring-1 ring-purple-500/30',
  moderator:  'bg-amber-500/20 text-amber-400 ring-1 ring-amber-500/30',
  super_admin:'bg-red-500/20 text-red-400 ring-1 ring-red-500/30',
};

const STATUS_STYLES: Record<string, string> = {
  active:               'bg-emerald-500/20 text-emerald-400 ring-1 ring-emerald-500/30',
  suspended:            'bg-red-500/20 text-red-400 ring-1 ring-red-500/30',
  pending_verification: 'bg-amber-500/20 text-amber-400 ring-1 ring-amber-500/30',
};

export function UserListTable({ users, onSuspend, onPromote }: UserListTableProps) {
  const t = useTranslations();

  if (users.length === 0) {
    return (
      <div className="rounded-2xl border border-white/10 bg-white/5 py-16 text-center">
        <p className="text-sm text-white/40">{t('admin.noUsers')}</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-2xl border border-white/10">
      <table className="min-w-full divide-y divide-white/10">
        <thead className="bg-white/5">
          <tr>
            {[
              t('admin.table.name'),
              t('admin.table.email'),
              t('admin.table.role'),
              t('admin.table.status'),
              t('admin.table.actions'),
            ].map((heading, i) => (
              <th
                key={heading}
                scope="col"
                className={`px-4 py-3 text-xs font-semibold uppercase tracking-widest text-white/40 ${i === 4 ? 'text-right' : 'text-left'}`}
              >
                {heading}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-white/5">
          {users.map((user) => (
            <tr key={user.id} className="transition hover:bg-white/5">
              <td className="whitespace-nowrap px-4 py-3.5 text-sm font-semibold text-white">
                {user.displayName}
              </td>
              <td className="whitespace-nowrap px-4 py-3.5 text-sm text-white/60">
                {user.email}
              </td>
              <td className="whitespace-nowrap px-4 py-3.5 text-sm">
                <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ${ROLE_STYLES[user.role] ?? 'bg-white/10 text-white/50'}`}>
                  {ROLE_LABELS[user.role] ?? user.role}
                </span>
              </td>
              <td className="whitespace-nowrap px-4 py-3.5 text-sm">
                <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold capitalize ${STATUS_STYLES[user.status] ?? 'bg-white/10 text-white/50'}`}>
                  {user.status.replace(/_/g, ' ')}
                </span>
              </td>
              <td className="whitespace-nowrap px-4 py-3.5 text-right text-sm">
                <div className="flex items-center justify-end gap-2">
                  {user.status !== 'suspended' && (
                    <button
                      type="button"
                      onClick={() => onSuspend(user)}
                      aria-label={`${t('admin.suspend')} ${user.displayName}`}
                      className="min-h-[36px] rounded-xl border border-red-500/30 bg-red-500/10 px-3 py-1.5 text-xs font-semibold text-red-400 transition hover:bg-red-500/20 focus:outline-none focus:ring-2 focus:ring-red-500/30"
                    >
                      {t('admin.suspend')}
                    </button>
                  )}
                  <button
                    type="button"
                    onClick={() => onPromote(user)}
                    aria-label={`${t('admin.promote')} ${user.displayName}`}
                    className="min-h-[36px] rounded-xl border border-blue-500/30 bg-blue-500/10 px-3 py-1.5 text-xs font-semibold text-blue-400 transition hover:bg-blue-500/20 focus:outline-none focus:ring-2 focus:ring-blue-500/30"
                  >
                    {t('admin.promote')}
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
