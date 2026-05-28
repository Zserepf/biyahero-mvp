/**
 * Internationalization configuration for BiyaHero.
 * Supports English (en) and Filipino/Tagalog (fil).
 * Default locale is Filipino unless the browser indicates English.
 *
 * This config is consumed by the next-intl provider (task 12.7)
 * and the language-preference store (task 12.8).
 */
export const i18nConfig = {
  locales: ["en", "fil"] as const,
  defaultLocale: "fil" as const,
} as const;

export type Locale = (typeof i18nConfig.locales)[number];

/**
 * Detect the user's preferred locale from the browser.
 * Returns "en" if the browser indicates English, otherwise "fil".
 * Safe to call on the server (returns defaultLocale).
 */
export function detectLocale(): Locale {
  if (typeof window === "undefined") return i18nConfig.defaultLocale;

  const stored = localStorage.getItem("biyahero-locale");
  if (stored === "en" || stored === "fil") return stored;

  const browserLang = navigator.language || "";
  return browserLang.startsWith("en") ? "en" : "fil";
}
