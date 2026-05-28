'use client';

/**
 * Registration form component.
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

const inputBase =
  'block w-full rounded-xl border px-4 py-3 text-sm text-white shadow-sm placeholder:text-white/30 transition-colors focus:outline-none focus:ring-2 focus:ring-offset-0';
const inputNormal = 'border-white/10 bg-white/5 focus:border-blue-500 focus:ring-blue-500/30';
const inputError = 'border-red-500/40 bg-red-500/10 focus:border-red-500 focus:ring-red-500/30';

function FieldError({ id, message }: { id: string; message: string }) {
  return (
    <p id={id} className="flex items-center gap-1 text-xs text-red-400">
      <svg className="h-3.5 w-3.5 shrink-0" fill="currentColor" viewBox="0 0 20 20">
        <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
      </svg>
      {message}
    </p>
  );
}

export function RegisterForm({ onSuccess }: RegisterFormProps) {
  const t = useTranslations();
  const { register, isLoading, error } = useRegister();
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);

  const [formData, setFormData] = useState<RegisterFormData>({
    email: '',
    password: '',
    confirmPassword: '',
    displayName: '',
    role: 'commuter',
  });
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof RegisterFormData, string>>>({});

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

  const EyeIcon = ({ open }: { open: boolean }) =>
    open ? (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
      </svg>
    ) : (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
        <path strokeLinecap="round" strokeLinejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
      </svg>
    );

  return (
    <form onSubmit={handleSubmit} className="space-y-5" noValidate>
      {/* Server error */}
      {error && (
        <div role="alert" className="flex items-start gap-3 rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          <svg className="mt-0.5 h-4 w-4 shrink-0" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
          </svg>
          {error}
        </div>
      )}

      {/* Display Name */}
      <div className="space-y-1.5">
        <label htmlFor="register-displayName" className="block text-sm font-medium text-white/80">
          {t('forms.displayName')}
        </label>
        <input
          id="register-displayName"
          type="text"
          autoComplete="name"
          required
          value={formData.displayName}
          onChange={(e) => handleChange('displayName', e.target.value)}
          placeholder={t('auth.displayNamePlaceholder')}
          aria-invalid={!!fieldErrors.displayName}
          aria-describedby={fieldErrors.displayName ? 'register-displayName-error' : undefined}
          className={`${inputBase} ${fieldErrors.displayName ? inputError : inputNormal}`}
        />
        {fieldErrors.displayName && (
          <FieldError id="register-displayName-error" message={fieldErrors.displayName} />
        )}
      </div>

      {/* Email */}
      <div className="space-y-1.5">
        <label htmlFor="register-email" className="block text-sm font-medium text-white/80">
          {t('forms.email')}
        </label>
        <input
          id="register-email"
          type="email"
          autoComplete="email"
          required
          value={formData.email}
          onChange={(e) => handleChange('email', e.target.value)}
          placeholder={t('auth.emailPlaceholder')}
          aria-invalid={!!fieldErrors.email}
          aria-describedby={fieldErrors.email ? 'register-email-error' : undefined}
          className={`${inputBase} ${fieldErrors.email ? inputError : inputNormal}`}
        />
        {fieldErrors.email && (
          <FieldError id="register-email-error" message={fieldErrors.email} />
        )}
      </div>

      {/* Password */}
      <div className="space-y-1.5">
        <label htmlFor="register-password" className="block text-sm font-medium text-white/80">
          {t('forms.password')}
        </label>
        <div className="relative">
          <input
            id="register-password"
            type={showPassword ? 'text' : 'password'}
            autoComplete="new-password"
            required
            value={formData.password}
            onChange={(e) => handleChange('password', e.target.value)}
            placeholder="Min. 8 characters"
            aria-invalid={!!fieldErrors.password}
            aria-describedby={fieldErrors.password ? 'register-password-error' : undefined}
            className={`${inputBase} pr-11 ${fieldErrors.password ? inputError : inputNormal}`}
          />
          <button
            type="button"
            onClick={() => setShowPassword((v) => !v)}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-white/30 hover:text-white/60 focus:outline-none"
            aria-label={showPassword ? 'Hide password' : 'Show password'}
          >
            <EyeIcon open={showPassword} />
          </button>
        </div>
        {fieldErrors.password && (
          <FieldError id="register-password-error" message={fieldErrors.password} />
        )}
      </div>

      {/* Confirm Password */}
      <div className="space-y-1.5">
        <label htmlFor="register-confirmPassword" className="block text-sm font-medium text-white/80">
          {t('forms.confirmPassword')}
        </label>
        <div className="relative">
          <input
            id="register-confirmPassword"
            type={showConfirm ? 'text' : 'password'}
            autoComplete="new-password"
            required
            value={formData.confirmPassword}
            onChange={(e) => handleChange('confirmPassword', e.target.value)}
            placeholder="Repeat your password"
            aria-invalid={!!fieldErrors.confirmPassword}
            aria-describedby={fieldErrors.confirmPassword ? 'register-confirmPassword-error' : undefined}
            className={`${inputBase} pr-11 ${fieldErrors.confirmPassword ? inputError : inputNormal}`}
          />
          <button
            type="button"
            onClick={() => setShowConfirm((v) => !v)}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-white/30 hover:text-white/60 focus:outline-none"
            aria-label={showConfirm ? 'Hide password' : 'Show password'}
          >
            <EyeIcon open={showConfirm} />
          </button>
        </div>
        {fieldErrors.confirmPassword && (
          <FieldError id="register-confirmPassword-error" message={fieldErrors.confirmPassword} />
        )}
      </div>

      {/* Role Selection */}
      <fieldset>
        <legend className="mb-2 block text-sm font-medium text-white/80">{t('forms.role')}</legend>
        <div className="grid grid-cols-2 gap-3">
          {(['commuter', 'driver'] as const).map((role) => {
            const isSelected = formData.role === role;
            return (
              <label
                key={role}
                className={`flex cursor-pointer items-center gap-3 rounded-xl border-2 px-4 py-3 transition-all ${
                  isSelected
                    ? 'border-blue-500 bg-blue-500/20 text-blue-300'
                    : 'border-white/10 bg-white/5 text-white/60 hover:border-white/20 hover:bg-white/10'
                }`}
              >
                <input
                  type="radio"
                  name="role"
                  value={role}
                  checked={isSelected}
                  onChange={() => handleChange('role', role)}
                  className="sr-only"
                />
                <span className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-full border-2 ${isSelected ? 'border-blue-500' : 'border-white/20'}`}>
                  {isSelected && <span className="h-2.5 w-2.5 rounded-full bg-blue-500" />}
                </span>
                <span className="text-sm font-medium">
                  {role === 'commuter' ? t('auth.roleCommuter') : t('auth.roleDriver')}
                </span>
              </label>
            );
          })}
        </div>
        {fieldErrors.role && (
          <p className="mt-1.5 flex items-center gap-1 text-xs text-red-400">
            <svg className="h-3.5 w-3.5" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
            </svg>
            {fieldErrors.role}
          </p>
        )}
      </fieldset>

      {/* Submit */}
      <button
        type="submit"
        disabled={isLoading}
        className="flex w-full items-center justify-center gap-2 rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white shadow-sm transition-all hover:bg-blue-700 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60 active:scale-[0.98]"
      >
        {isLoading ? (
          <>
            <span className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
            <span>Creating account...</span>
          </>
        ) : (
          t('auth.register')
        )}
      </button>
    </form>
  );
}
