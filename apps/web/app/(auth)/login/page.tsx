'use client';

/**
 * Login page — POST /v1/auth/sessions.
 * Requirements: 5.3, 5.6, 9.1, 9.2, 9.4, 10.1
 */

import { useState, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { useLogin } from '@/features/auth/useLogin';
import { loginSchema, type LoginFormData } from '@/features/auth/schema';
import { apiClient } from '@/infrastructure/api/client';
import Link from 'next/link';

export default function LoginPage() {
  const t = useTranslations();
  const router = useRouter();
  const searchParams = useSearchParams();
  const justRegistered = searchParams.get('registered') === 'true';
  const { login, isLoading, error } = useLogin();
  const [showPassword, setShowPassword] = useState(false);
  const [devPanelOpen, setDevPanelOpen] = useState(false);
  const [devForm, setDevForm] = useState({ email: '', password: '', role: 'super_admin', displayName: '' });
  const [devStatus, setDevStatus] = useState<string | null>(null);
  const [devLoading, setDevLoading] = useState(false);

  // Secret key combo: type "devmode" anywhere on the page to open dev panel
  useEffect(() => {
    let typed = '';
    const handler = (e: KeyboardEvent) => {
      if (!e.key) return;
      typed += e.key.toLowerCase();
      if (typed.length > 7) typed = typed.slice(-7);
      if (typed === 'devmode') setDevPanelOpen(true);
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, []);

  async function handleDevSeed(e: React.FormEvent) {
    e.preventDefault();
    setDevLoading(true);
    setDevStatus(null);
    try {
      const res = await apiClient.post('/v1/dev/seed-role', devForm);
      setDevStatus(`✓ ${(res.data as { message: string }).message}`);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed';
      setDevStatus(`✗ ${msg}`);
    } finally {
      setDevLoading(false);
    }
  }

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
      router.push('/');
    } catch {
      // Error handled by hook
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-gradient-to-br from-blue-50 via-white to-indigo-50 px-4 py-8">
      <div className="w-full max-w-md">

        {/* Brand */}
        <div className="mb-8 text-center">
          <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-blue-600 shadow-lg">
            <svg className="h-8 w-8 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
          <h1 className="text-3xl font-bold tracking-tight text-gray-900">BiyaHero</h1>
          <p className="mt-1 text-sm text-gray-500">{t('auth.loginSubtitle')}</p>
        </div>

        {/* Card */}
        <div className="rounded-2xl bg-white px-8 py-8 shadow-xl ring-1 ring-gray-100">
          <h2 className="mb-6 text-xl font-semibold text-gray-800">{t('auth.login')}</h2>

          {/* Registration success banner */}
          {justRegistered && (
            <div className="mb-5 flex items-start gap-3 rounded-xl bg-green-50 px-4 py-3 text-sm text-green-700 ring-1 ring-green-200">
              <svg className="mt-0.5 h-4 w-4 shrink-0" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
              </svg>
              <span>Account created successfully! You can now log in.</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-5" noValidate>
            {/* Server error */}
            {error && (
              <div role="alert" className="flex items-start gap-3 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700 ring-1 ring-red-200">
                <svg className="mt-0.5 h-4 w-4 shrink-0" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
                </svg>
                {error}
              </div>
            )}

            {/* Email */}
            <div className="space-y-1.5">
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
                className={`block w-full rounded-xl border px-4 py-3 text-sm text-gray-900 shadow-sm placeholder:text-gray-400 transition-colors focus:outline-none focus:ring-2 focus:ring-offset-0 ${
                  fieldErrors.email
                    ? 'border-red-400 bg-red-50 focus:border-red-400 focus:ring-red-400'
                    : 'border-gray-200 bg-gray-50 focus:border-blue-500 focus:bg-white focus:ring-blue-500'
                }`}
              />
              {fieldErrors.email && (
                <p id="login-email-error" className="flex items-center gap-1 text-xs text-red-600">
                  <svg className="h-3.5 w-3.5 shrink-0" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
                  </svg>
                  {fieldErrors.email}
                </p>
              )}
            </div>

            {/* Password */}
            <div className="space-y-1.5">
              <label htmlFor="login-password" className="block text-sm font-medium text-gray-700">
                {t('forms.password')}
              </label>
              <div className="relative">
                <input
                  id="login-password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  required
                  value={formData.password}
                  onChange={(e) => handleChange('password', e.target.value)}
                  placeholder={t('auth.passwordPlaceholder')}
                  aria-invalid={!!fieldErrors.password}
                  aria-describedby={fieldErrors.password ? 'login-password-error' : undefined}
                  className={`block w-full rounded-xl border px-4 py-3 pr-11 text-sm text-gray-900 shadow-sm placeholder:text-gray-400 transition-colors focus:outline-none focus:ring-2 focus:ring-offset-0 ${
                    fieldErrors.password
                      ? 'border-red-400 bg-red-50 focus:border-red-400 focus:ring-red-400'
                      : 'border-gray-200 bg-gray-50 focus:border-blue-500 focus:bg-white focus:ring-blue-500'
                  }`}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((v) => !v)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 focus:outline-none"
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                >
                  {showPassword ? (
                    <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
                    </svg>
                  ) : (
                    <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                      <path strokeLinecap="round" strokeLinejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                    </svg>
                  )}
                </button>
              </div>
              {fieldErrors.password && (
                <p id="login-password-error" className="flex items-center gap-1 text-xs text-red-600">
                  <svg className="h-3.5 w-3.5 shrink-0" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
                  </svg>
                  {fieldErrors.password}
                </p>
              )}
            </div>

            {/* Submit */}
            <button
              type="submit"
              disabled={isLoading}
              className="flex w-full items-center justify-center gap-2 rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white shadow-sm transition-all hover:bg-blue-700 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60 active:scale-[0.98]"
            >
              {isLoading ? (
                <>
                  <span className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                  <span>Signing in...</span>
                </>
              ) : (
                t('auth.login')
              )}
            </button>
          </form>
        </div>

        {/* Register link */}
        <p className="mt-6 text-center text-sm text-gray-500">
          {t('auth.noAccount')}{' '}
          <Link
            href="/register"
            className="font-semibold text-blue-600 hover:text-blue-500 focus:outline-none focus:underline"
          >
            {t('nav.register')}
          </Link>
        </p>
      </div>

      {/* Hidden Dev Panel — type "devmode" to open */}
      {devPanelOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
          <div className="w-full max-w-sm rounded-2xl bg-slate-900 p-6 shadow-2xl ring-1 ring-white/10">
            <div className="mb-4 flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-white">🔧 Dev Panel</h3>
                <p className="text-xs text-white/40">Create or promote accounts (dev only)</p>
              </div>
              <button onClick={() => setDevPanelOpen(false)} className="text-white/40 hover:text-white">
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
            <form onSubmit={handleDevSeed} className="space-y-3">
              <input
                type="text"
                placeholder="Display Name"
                value={devForm.displayName}
                onChange={e => setDevForm(f => ({ ...f, displayName: e.target.value }))}
                className="w-full rounded-lg bg-white/10 px-3 py-2 text-sm text-white placeholder:text-white/30 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
              <input
                type="email"
                placeholder="Email"
                required
                value={devForm.email}
                onChange={e => setDevForm(f => ({ ...f, email: e.target.value }))}
                className="w-full rounded-lg bg-white/10 px-3 py-2 text-sm text-white placeholder:text-white/30 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
              <input
                type="password"
                placeholder="Password"
                required
                value={devForm.password}
                onChange={e => setDevForm(f => ({ ...f, password: e.target.value }))}
                className="w-full rounded-lg bg-white/10 px-3 py-2 text-sm text-white placeholder:text-white/30 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
              <select
                value={devForm.role}
                onChange={e => setDevForm(f => ({ ...f, role: e.target.value }))}
                className="w-full rounded-lg bg-white/10 px-3 py-2 text-sm text-white focus:outline-none focus:ring-1 focus:ring-blue-500"
              >
                <option value="commuter">Commuter</option>
                <option value="driver">Driver</option>
                <option value="moderator">Moderator</option>
                <option value="SuperAdmin">Super Admin</option>
              </select>
              {devStatus && (
                <p className={`text-xs ${devStatus.startsWith('✓') ? 'text-green-400' : 'text-red-400'}`}>
                  {devStatus}
                </p>
              )}
              <button
                type="submit"
                disabled={devLoading}
                className="w-full rounded-lg bg-blue-600 py-2 text-sm font-semibold text-white hover:bg-blue-500 disabled:opacity-50"
              >
                {devLoading ? 'Creating...' : 'Create / Promote Account'}
              </button>
            </form>
          </div>
        </div>
      )}
    </main>
  );
}
