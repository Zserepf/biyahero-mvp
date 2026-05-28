'use client';

/**
 * Registration form component.
 *
 * Handles form state, Zod validation, and calls the register mutation.
 * Displays inline validation errors.
 * Requirements: 5.1, 9.1, 9.2, 9.4, 10.1
 */

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { registerSchema, type RegisterFormData } from './schema';
import { useRegister } from './useRegister';
import type { RegisterResponse } from './types';

interface RegisterFormProps {
  onSuccess?: (response: RegisterResponse) => void;
}

export function RegisterForm({ onSuccess }: RegisterFormProps) {
  const t = useTranslations();
  const { register, isLoading, error } = useRegister();

  const [formData, setFormData] = useState<RegisterFormData>({
    email: '',
    password: '',
    confirmPassword: '',
    displayName: '',
    role: 'commuter',
  });
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof RegisterFormData, string>>>(
    {},
  );

  function handleChange(field: keyof RegisterFormData, value: string) {
    setFormData((prev) => ({ ...prev, [field]: value }));
    if (fieldErrors[field]) {
      setFieldErrors((prev) => ({ ...prev, [field]: undefined }));
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    const result = registerSchema.safeParse(formData);
    if (!result.success) {
      const errors: Partial<Record<keyof RegisterFormData, string>> = {};
      for (const issue of result.error.issues) {
        const field = issue.path[0] as keyof RegisterFormData;
        if (!errors[field]) {
          errors[field] = t(issue.message);
        }
      }
      setFieldErrors(errors);
      return;
    }

    try {
      const response = await register({
        email: formData.email,
        password: formData.password,
        displayName: formData.displayName,
        role: formData.role,
      });
      onSuccess?.(response);
    } catch {
      // Error is handled by the hook
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4" noValidate>
      {/* Server error */}
      {error && (
        <div role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Display Name */}
      <div>
        <label htmlFor="register-displayName" className="block text-sm font-medium text-gray-700">
          {t('forms.displayName')}
        </label>
        <input
          id="register-displayName"
          type="text"
          autoComplete="name"
          required
          value={formData.displayName}
          onChange={(e) => handleChange('displayName', e.target.value)}
          aria-invalid={!!fieldErrors.displayName}
          aria-describedby={fieldErrors.displayName ? 'register-displayName-error' : undefined}
          className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2.5 text-base shadow-sm placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        {fieldErrors.displayName && (
          <p id="register-displayName-error" className="mt-1 text-sm text-red-600">
            {fieldErrors.displayName}
          </p>
        )}
      </div>

      {/* Email */}
      <div>
        <label htmlFor="register-email" className="block text-sm font-medium text-gray-700">
          {t('forms.email')}
        </label>
        <input
          id="register-email"
          type="email"
          autoComplete="email"
          required
          value={formData.email}
          onChange={(e) => handleChange('email', e.target.value)}
          aria-invalid={!!fieldErrors.email}
          aria-describedby={fieldErrors.email ? 'register-email-error' : undefined}
          className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2.5 text-base shadow-sm placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        {fieldErrors.email && (
          <p id="register-email-error" className="mt-1 text-sm text-red-600">
            {fieldErrors.email}
          </p>
        )}
      </div>

      {/* Password */}
      <div>
        <label htmlFor="register-password" className="block text-sm font-medium text-gray-700">
          {t('forms.password')}
        </label>
        <input
          id="register-password"
          type="password"
          autoComplete="new-password"
          required
          value={formData.password}
          onChange={(e) => handleChange('password', e.target.value)}
          aria-invalid={!!fieldErrors.password}
          aria-describedby={fieldErrors.password ? 'register-password-error' : undefined}
          className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2.5 text-base shadow-sm placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        {fieldErrors.password && (
          <p id="register-password-error" className="mt-1 text-sm text-red-600">
            {fieldErrors.password}
          </p>
        )}
      </div>

      {/* Confirm Password */}
      <div>
        <label
          htmlFor="register-confirmPassword"
          className="block text-sm font-medium text-gray-700"
        >
          {t('forms.confirmPassword')}
        </label>
        <input
          id="register-confirmPassword"
          type="password"
          autoComplete="new-password"
          required
          value={formData.confirmPassword}
          onChange={(e) => handleChange('confirmPassword', e.target.value)}
          aria-invalid={!!fieldErrors.confirmPassword}
          aria-describedby={
            fieldErrors.confirmPassword ? 'register-confirmPassword-error' : undefined
          }
          className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2.5 text-base shadow-sm placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        {fieldErrors.confirmPassword && (
          <p id="register-confirmPassword-error" className="mt-1 text-sm text-red-600">
            {fieldErrors.confirmPassword}
          </p>
        )}
      </div>

      {/* Role Selection */}
      <fieldset>
        <legend className="block text-sm font-medium text-gray-700">{t('forms.role')}</legend>
        <div className="mt-2 flex gap-4">
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="radio"
              name="role"
              value="commuter"
              checked={formData.role === 'commuter'}
              onChange={() => handleChange('role', 'commuter')}
              className="h-4 w-4 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-sm text-gray-700">{t('auth.roleCommuter')}</span>
          </label>
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="radio"
              name="role"
              value="driver"
              checked={formData.role === 'driver'}
              onChange={() => handleChange('role', 'driver')}
              className="h-4 w-4 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-sm text-gray-700">{t('auth.roleDriver')}</span>
          </label>
        </div>
        {fieldErrors.role && (
          <p className="mt-1 text-sm text-red-600">{fieldErrors.role}</p>
        )}
      </fieldset>

      {/* Submit */}
      <button
        type="submit"
        disabled={isLoading}
        className="flex w-full items-center justify-center rounded-md bg-blue-600 px-4 py-2.5 text-base font-medium text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
      >
        {isLoading ? (
          <span className="h-5 w-5 animate-spin rounded-full border-2 border-white border-t-transparent" />
        ) : (
          t('auth.register')
        )}
      </button>
    </form>
  );
}
