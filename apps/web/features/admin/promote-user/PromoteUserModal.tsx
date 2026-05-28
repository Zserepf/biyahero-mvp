'use client';

/**
 * Promote user modal with password 2FA confirmation.
 *
 * Before a role change is submitted, the Super Admin must re-enter their own
 * password as a second-factor confirmation (Req 5.11).
 * Requirements: 5.9, 5.11, 9.1, 9.4
 */

import { useState, useEffect, useRef } from 'react';
import { useTranslations } from 'next-intl';
import { promoteUserSchema, type PromoteUserFormData } from './schema';
import { usePromoteUser } from './usePromoteUser';
import type { AdminUserItem } from '../user-list/types';
import type { UserRole } from './types';

interface PromoteUserModalProps {
  user: AdminUserItem;
  onClose: () => void;
  onSuccess: () => void;
}

const AVAILABLE_ROLES: { value: UserRole; label: string }[] = [
  { value: 'commuter', label: 'Commuter' },
  { value: 'driver', label: 'Driver' },
  { value: 'moderator', label: 'Moderator' },
  { value: 'super_admin', label: 'Super Admin' },
];

export function PromoteUserModal({ user, onClose, onSuccess }: PromoteUserModalProps) {
  const t = useTranslations();
  const { promoteUser, isLoading, error } = usePromoteUser();
  const passwordRef = useRef<HTMLInputElement>(null);

  const [formData, setFormData] = useState<PromoteUserFormData>({
    password: '',
    newRole: user.role === 'commuter' ? 'driver' : user.role === 'driver' ? 'moderator' : 'super_admin',
  });
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof PromoteUserFormData, string>>>({});

  // Focus the password input on mount
  useEffect(() => {
    passwordRef.current?.focus();
  }, []);

  // Close on Escape key
  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape' && !isLoading) {
        onClose();
      }
    }
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [onClose, isLoading]);

  function handleChange(field: keyof PromoteUserFormData, value: string) {
    setFormData((prev) => ({ ...prev, [field]: value }));
    if (fieldErrors[field]) {
      setFieldErrors((prev) => ({ ...prev, [field]: undefined }));
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    const result = promoteUserSchema.safeParse(formData);
    if (!result.success) {
      const errors: Partial<Record<keyof PromoteUserFormData, string>> = {};
      for (const issue of result.error.issues) {
        const field = issue.path[0] as keyof PromoteUserFormData;
        if (!errors[field]) {
          errors[field] = t(issue.message);
        }
      }
      setFieldErrors(errors);
      return;
    }

    try {
      await promoteUser(user.id, {
        password: formData.password,
        newRole: formData.newRole,
      });
      onSuccess();
    } catch {
      // Error is displayed in the modal
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="promote-dialog-title"
    >
      <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
        <h2
          id="promote-dialog-title"
          className="text-lg font-semibold text-gray-900"
        >
          {t('admin.promoteTitle')}
        </h2>

        <p className="mt-2 text-sm text-gray-600">
          {t('admin.promoteMessage', { name: user.displayName })}
        </p>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4" noValidate>
          {/* Server error */}
          {error && (
            <div role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">
              {error}
            </div>
          )}

          {/* Role selection */}
          <div>
            <label
              htmlFor="promote-role"
              className="block text-sm font-medium text-gray-700"
            >
              {t('admin.newRole')}
            </label>
            <select
              id="promote-role"
              value={formData.newRole}
              onChange={(e) => handleChange('newRole', e.target.value)}
              aria-invalid={!!fieldErrors.newRole}
              aria-describedby={fieldErrors.newRole ? 'promote-role-error' : undefined}
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2.5 text-base shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            >
              {AVAILABLE_ROLES.filter((r) => r.value !== user.role).map((role) => (
                <option key={role.value} value={role.value}>
                  {role.label}
                </option>
              ))}
            </select>
            {fieldErrors.newRole && (
              <p id="promote-role-error" className="mt-1 text-sm text-red-600">
                {fieldErrors.newRole}
              </p>
            )}
          </div>

          {/* Password 2FA */}
          <div>
            <label
              htmlFor="promote-password"
              className="block text-sm font-medium text-gray-700"
            >
              {t('admin.reenterPassword')}
            </label>
            <p className="mt-0.5 text-xs text-gray-500">
              {t('admin.reenterPasswordHint')}
            </p>
            <input
              ref={passwordRef}
              id="promote-password"
              type="password"
              autoComplete="current-password"
              required
              value={formData.password}
              onChange={(e) => handleChange('password', e.target.value)}
              placeholder={t('admin.passwordPlaceholder')}
              aria-invalid={!!fieldErrors.password}
              aria-describedby={fieldErrors.password ? 'promote-password-error' : undefined}
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2.5 text-base shadow-sm placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
            {fieldErrors.password && (
              <p id="promote-password-error" className="mt-1 text-sm text-red-600">
                {fieldErrors.password}
              </p>
            )}
          </div>

          {/* Actions */}
          <div className="flex items-center justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              disabled={isLoading}
              className="min-h-[44px] min-w-[44px] rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50"
            >
              {t('common.cancel')}
            </button>
            <button
              type="submit"
              disabled={isLoading}
              className="min-h-[44px] min-w-[44px] rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50"
            >
              {isLoading ? (
                <span className="inline-flex items-center gap-2">
                  <span className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                  {t('admin.promoting')}
                </span>
              ) : (
                t('admin.confirmPromote')
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
