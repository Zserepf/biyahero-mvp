'use client';

/**
 * Login form component.
 *
 * Handles form state, Zod validation, and calls the login mutation.
 * Displays inline validation errors.
 * Requirements: 5.3, 5.6, 9.1, 9.2, 9.4, 10.1
 */

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { loginSchema, type LoginFormData } from './schema';
import { useLogin } from './useLogin';

interface LoginFormProps {
  onSuccess?: () => void;
}

export function LoginForm({ onSuccess }: LoginFormProps) {
  const t = useTranslations();
  const { login, isLoading, error } = useLogin();

  const [formData, setFormData] = useState<LoginFormData>({
    email: '',
    password: '',
  });
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof LoginFormData, string>>>({});

  function handleChange(field: keyof LoginFormData, value: string) {
    setFormData((prev) => ({ ...prev, [field]: value }));
    if (fieldErrors[field]) {
      setFieldErrors((prev) => ({ ...prev, [field]: undefined }));
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    const result = loginSchema.safeParse(formData);
    if (!result.success) {
      const errors: Partial<Record<keyof LoginFormData, string>> = {};
      for (const issue of result.error.issues) {
        const field = issue.path[0] as keyof LoginFormData;
        if (!errors[field]) {
          errors[field] = t(issue.message);
        }
      }
      setFieldErrors(errors);
      return;
    }

    try {
      await login({ email: formData.email, password: formData.password });
      onSuccess?.();
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

      {/* Email */}
      <div>
        <label htmlFor="login-email" className="block text-sm font-medium text-gray-700">
          {t('forms.email')}
        </label>
        <input
          id="login-email"
          type="email"
          autoComplete="email"
          required
          value={formData.email}
          onChange={(e) => handleChange('email', e.target.value)}
          placeholder={t('auth.emailPlaceholder')}
          aria-invalid={!!fieldErrors.email}
          aria-describedby={fieldErrors.email ? 'login-email-error' : undefined}
          className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2.5 text-base shadow-sm placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        {fieldErrors.email && (
          <p id="login-email-error" className="mt-1 text-sm text-red-600">
            {fieldErrors.email}
          </p>
        )}
      </div>

      {/* Password */}
      <div>
        <label htmlFor="login-password" className="block text-sm font-medium text-gray-700">
          {t('forms.password')}
        </label>
        <input
          id="login-password"
          type="password"
          autoComplete="current-password"
          required
          value={formData.password}
          onChange={(e) => handleChange('password', e.target.value)}
          placeholder={t('auth.passwordPlaceholder')}
          aria-invalid={!!fieldErrors.password}
          aria-describedby={fieldErrors.password ? 'login-password-error' : undefined}
          className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2.5 text-base shadow-sm placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        {fieldErrors.password && (
          <p id="login-password-error" className="mt-1 text-sm text-red-600">
            {fieldErrors.password}
          </p>
        )}
      </div>

      {/* Submit */}
      <button
        type="submit"
        disabled={isLoading}
        className="flex w-full items-center justify-center rounded-md bg-blue-600 px-4 py-2.5 text-base font-medium text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
      >
        {isLoading ? (
          <span className="h-5 w-5 animate-spin rounded-full border-2 border-white border-t-transparent" />
        ) : (
          t('auth.login')
        )}
      </button>
    </form>
  );
}
