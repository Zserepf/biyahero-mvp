'use client';

/**
 * Email verification form component.
 *
 * Reads the token from URL search params and triggers verification automatically.
 * Shows status (loading, success, error) to the user.
 * Requirements: 5.2, 9.1, 9.2
 */

import { useEffect, useRef } from 'react';
import { useTranslations } from 'next-intl';
import { useVerifyEmail } from './useVerifyEmail';

interface VerifyEmailFormProps {
  token: string | null;
  onSuccess?: () => void;
}

export function VerifyEmailForm({ token, onSuccess }: VerifyEmailFormProps) {
  const t = useTranslations();
  const { verifyEmail, isLoading, error, isSuccess } = useVerifyEmail();
  const hasAttempted = useRef(false);

  useEffect(() => {
    if (token && !hasAttempted.current) {
      hasAttempted.current = true;
      verifyEmail(token)
        .then(() => onSuccess?.())
        .catch(() => {
          // Error is handled by the hook
        });
    }
  }, [token, verifyEmail, onSuccess]);

  if (!token) {
    return (
      <div role="alert" className="rounded-md bg-yellow-50 p-4 text-sm text-yellow-800">
        {t('auth.verifyEmailMissingToken')}
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex flex-col items-center gap-3" role="status" aria-label="Verifying">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-blue-600 border-t-transparent" />
        <p className="text-sm text-gray-600">{t('auth.verifyEmailLoading')}</p>
      </div>
    );
  }

  if (isSuccess) {
    return (
      <div role="status" className="rounded-md bg-green-50 p-4 text-sm text-green-700">
        {t('auth.verifyEmailSuccess')}
      </div>
    );
  }

  if (error) {
    return (
      <div role="alert" className="rounded-md bg-red-50 p-4 text-sm text-red-700">
        {error}
      </div>
    );
  }

  return null;
}
