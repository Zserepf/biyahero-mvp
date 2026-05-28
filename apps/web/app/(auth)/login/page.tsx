'use client';

/**
 * Login page — POST /v1/auth/sessions.
 *
 * Mobile-first, accessible login form with i18n support.
 * Requirements: 5.3, 5.6, 9.1, 9.2, 9.4, 10.1
 */

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { useLogin } from '@/features/auth/useLogin';
import { loginSchema, type LoginFormData } from '@/features/auth/schema';
import Link from 'next/link';

export default function LoginPage() {
  const t = useTranslations();
  const router = useRouter();
  const { login, isLoading, error } = useLogin();

  const [formData, setFormData] = useState<LoginFormData>({
    email: '',
    password: '',
  });
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof LoginFormData, string>>>({});

  function handleChange(field: keyof LoginFormData, value: string) {
    setFormData((prev) => ({ ...prev, [field]: value }));
    // Clear field error on change
    if (fieldErrors[field]) {
      setFieldErrors((prev) => ({ ...prev, [field]: undefined }));
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    // Client-side validation
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
      router.push('/');
    } catch {
      // Error is handled by the hook and displayed below
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-8">
      <div className="w-full max-w-sm space-y-6">
        {/* Header */}
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900">
            {t('auth.login')}
          </h1>
          <p className="mt-1 text-sm text-gray-600">
            {t('auth.loginSubtitle')}
          </p>
        </div>

        {/* Server error */}
        {error && (
          <div
            role="alert"
            className="rounded-md bg-red-50 p-3 text-sm text-red-700"
          >
            {error}
          </div>
        )}

        {/* Form */}
        <form onSubmit={handleSubmit} className="space-y-4" noValidate>
          {/* Email */}
          <div>
            <label
              htmlFor="login-email"
              className="block text-sm font-medium text-gray-700"
            >
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
            <label
              htmlFor="login-password"
              className="block text-sm font-medium text-gray-700"
            >
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

        {/* Register link */}
        <p className="text-center text-sm text-gray-600">
          {t('auth.noAccount')}{' '}
          <Link
            href="/register"
            className="font-medium text-blue-600 hover:text-blue-500 focus:outline-none focus:underline"
          >
            {t('nav.register')}
          </Link>
        </p>
      </div>
    </main>
  );
}
