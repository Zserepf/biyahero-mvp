'use client';

/**
 * Language preference settings page — route target for /settings/language.
 *
 * Thin page component that composes the LanguagePreferenceForm.
 * Requirements: 10.3
 */

import { useTranslations } from 'next-intl';
import { LanguagePreferenceForm } from './LanguagePreferenceForm';

export function LanguagePreferencePage() {
  const t = useTranslations();

  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-8">
      <div className="w-full max-w-sm space-y-6">
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900">{t('settings.languageTitle')}</h1>
          <p className="mt-1 text-sm text-gray-600">{t('settings.languageSubtitle')}</p>
        </div>

        <LanguagePreferenceForm />
      </div>
    </main>
  );
}
