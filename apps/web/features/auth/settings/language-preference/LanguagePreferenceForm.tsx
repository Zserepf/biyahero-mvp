'use client';

/**
 * Language preference form component.
 *
 * A toggle between English and Filipino (Tagalog).
 * Updates happen instantly via the Zustand store — no page reload needed.
 * Requirements: 10.3, 9.1, 9.4
 */

import { useTranslations } from 'next-intl';
import { useLanguagePreference } from './useLanguagePreference';
import type { LanguagePreference } from './types';

export function LanguagePreferenceForm() {
  const t = useTranslations();
  const { currentLanguage, setLanguage } = useLanguagePreference();

  function handleChange(lang: LanguagePreference) {
    setLanguage(lang);
  }

  return (
    <fieldset className="space-y-3">
      <legend className="block text-sm font-medium text-gray-700">
        {t('settings.language')}
      </legend>

      <div className="flex gap-3">
        <button
          type="button"
          onClick={() => handleChange('en')}
          aria-pressed={currentLanguage === 'en'}
          className={`flex-1 rounded-md px-4 py-2.5 text-base font-medium shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 ${
            currentLanguage === 'en'
              ? 'bg-blue-600 text-white'
              : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
          }`}
        >
          English
        </button>
        <button
          type="button"
          onClick={() => handleChange('fil')}
          aria-pressed={currentLanguage === 'fil'}
          className={`flex-1 rounded-md px-4 py-2.5 text-base font-medium shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 ${
            currentLanguage === 'fil'
              ? 'bg-blue-600 text-white'
              : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
          }`}
        >
          Filipino
        </button>
      </div>

      <p className="text-xs text-gray-500">{t('settings.languageHint')}</p>
    </fieldset>
  );
}
