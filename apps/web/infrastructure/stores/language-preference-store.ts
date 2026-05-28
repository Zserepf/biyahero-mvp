/**
 * Language-preference Zustand store with localStorage mirror and server sync.
 *
 * - Stores the current locale ("en" | "fil")
 * - Mirrors to localStorage on every change (offline-resilient)
 * - Dispatches a custom event so the I18nProvider picks up changes immediately
 * - Fire-and-forget PATCH to server when authenticated
 * - On login, syncs from server-stored preference
 *
 * Requirements: 10.3, 10.4, 10.5
 */

import { create } from "zustand";
import { type Locale, i18nConfig, detectLocale } from "@/i18n.config";
import { apiClient } from "@/infrastructure/api/client";
import { API_ENDPOINTS } from "@/infrastructure/api/endpoints";

const LOCALE_STORAGE_KEY = "biyahero-locale";
const LOCALE_CHANGE_EVENT = "biyahero-locale-change";
const ACCESS_TOKEN_KEY = "biyahero_access_token";

// ─── Types ───────────────────────────────────────────────────────────────────

interface LanguagePreferenceState {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  syncFromServer: () => Promise<void>;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

/**
 * Check if the user is currently authenticated (has a stored access token).
 */
function isAuthenticated(): boolean {
  if (typeof window === "undefined") return false;
  return !!localStorage.getItem(ACCESS_TOKEN_KEY);
}

/**
 * Persist locale to localStorage.
 */
function persistToLocalStorage(locale: Locale): void {
  if (typeof window === "undefined") return;
  localStorage.setItem(LOCALE_STORAGE_KEY, locale);
}

/**
 * Dispatch a custom event so the I18nProvider updates the UI immediately.
 * This ensures UI updates within 100ms without a page reload (Req 10.3).
 */
function dispatchLocaleChangeEvent(locale: Locale): void {
  if (typeof window === "undefined") return;
  window.dispatchEvent(
    new CustomEvent(LOCALE_CHANGE_EVENT, { detail: { locale } })
  );
}

/**
 * Fire-and-forget PATCH to persist the language preference on the server.
 * Only fires when the user is authenticated.
 */
function syncToServer(locale: Locale): void {
  if (!isAuthenticated()) return;

  apiClient
    .patch(API_ENDPOINTS.AUTH.LANGUAGE_PREFERENCE, { languagePreference: locale })
    .catch(() => {
      // Fire-and-forget — swallow errors silently.
      // The localStorage mirror ensures the preference persists locally.
    });
}

// ─── Store ───────────────────────────────────────────────────────────────────

export const useLanguagePreferenceStore = create<LanguagePreferenceState>(
  (set, get) => ({
    locale: typeof window !== "undefined" ? detectLocale() : i18nConfig.defaultLocale,

    setLocale: (locale: Locale) => {
      if (!i18nConfig.locales.includes(locale)) return;
      if (locale === get().locale) return;

      set({ locale });
      persistToLocalStorage(locale);
      dispatchLocaleChangeEvent(locale);
      syncToServer(locale);
    },

    syncFromServer: async () => {
      if (!isAuthenticated()) return;

      try {
        const response = await apiClient.get<{ languagePreference: Locale }>(
          API_ENDPOINTS.AUTH.ME
        );

        const serverLocale = response.data.languagePreference;

        if (
          serverLocale &&
          i18nConfig.locales.includes(serverLocale) &&
          serverLocale !== get().locale
        ) {
          set({ locale: serverLocale });
          persistToLocalStorage(serverLocale);
          dispatchLocaleChangeEvent(serverLocale);
        }
      } catch {
        // If the server is unreachable, keep the local preference.
        // The localStorage mirror ensures continuity (Req 10.5).
      }
    },
  })
);
