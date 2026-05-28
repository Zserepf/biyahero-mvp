'use client';

/**
 * Language preference hook — PATCH /v1/auth/me/language-preference.
 *
 * Updates the user's language preference both locally (Zustand + localStorage)
 * and on the server. UI updates within 100ms without page reload (Req 10.3).
 * Requirements: 10.3
 */

import { useCallback } from 'react';
import { useLanguagePreferenceStore } from '@/infrastructure/stores/language-preference-store';
import type { LanguagePreference } from './types';

interface UseLanguagePreferenceReturn {
  currentLanguage: LanguagePreference;
  setLanguage: (lang: LanguagePreference) => void;
}

export function useLanguagePreference(): UseLanguagePreferenceReturn {
  const locale = useLanguagePreferenceStore((s) => s.locale);
  const setLocale = useLanguagePreferenceStore((s) => s.setLocale);

  const setLanguage = useCallback(
    (lang: LanguagePreference) => {
      setLocale(lang);
    },
    [setLocale],
  );

  return {
    currentLanguage: locale,
    setLanguage,
  };
}
