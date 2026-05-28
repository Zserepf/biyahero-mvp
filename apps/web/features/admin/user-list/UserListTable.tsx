'use client';

/**
 * Admin user list table component.
 *
 * Displays all users in a responsive table with id, email, role, status, and displayName.
 * Provides action buttons for suspend and promote operations.
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

const STATUS_STYLES: Record<string, string> = {
  active: 'bg-green-100 text-green-800',
  suspended: 'bg-red-100 text-red-800',
  pending_verification: 'bg-yellow-100 text-yellow-800',
};

export function UserListTable({ users, onSuspend, onPromote }: UserListTableProps) {
  const t = useTranslations();

  if (users.length === 0) {
    return (
      <p className="py-8 text-center text-gray-500">
        {t('admin.noUsers')}
      </p>
    );
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200">
      <table className="min-w-full divide-y divide-gray-200">
        <thead className="bg-gray-50">
          <tr>
            <th
              scope="col"
              className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500"
            >
              {t('admin.table.name')}
            </th>
            <th
              scope="col"
              className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500"
            >
              {t('admin.table.email')}
            </th>
            <th
              scope="col"
              className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500"
            >
              {t('admin.table.role')}
            </th>
            <th
              scope="col"
              className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500"
            >
              {t('admin.table.status')}
            </th>
            <th
              scope="col"
              className="px-4 py-3 text-right text-xs font-medium uppercase tracking-wider text-gray-500"
            >
              {t('admin.table.actions')}
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-200 bg-white">
          {users.map((user) => (
            <tr key={user.id} className="hover:bg-gray-50">
              <td className="whitespace-nowrap px-4 py-3 text-sm font-medium text-gray-900">
                {user.displayName}
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">
                {user.email}
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">
                {ROLE_LABELS[user.role] ?? user.role}
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm">
                <span
                  className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_STYLES[user.status] ?? 'bg-gray-100 text-gray-800'}`}
                >
                  {user.status.replace('_', ' ')}
                </span>
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-right text-sm">
                <div className="flex items-center justify-end gap-2">
                  {user.status !== 'suspended' && (
                    <button
                      type="button"
                      onClick={() => onSuspend(user)}
                      aria-label={`${t('admin.suspend')} ${user.displayName}`}
                      className="min-h-[44px] min-w-[44px] rounded-md border border-red-300 px-3 py-2 text-xs font-medium text-red-700 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-500 focus:ring-offset-2"
                    >
                      {t('admin.suspend')}
                    </button>
                  )}
                  <button
                    type="button"
                    onClick={() => onPromote(user)}
                    aria-label={`${t('admin.promote')} ${user.displayName}`}
                    className="min-h-[44px] min-w-[44px] rounded-md border border-blue-300 px-3 py-2 text-xs font-medium text-blue-700 hover:bg-blue-50 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
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
