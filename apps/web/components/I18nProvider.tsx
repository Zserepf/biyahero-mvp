"use client";

import { NextIntlClientProvider } from "next-intl";
import { useEffect, useState } from "react";
import { i18nConfig, detectLocale, type Locale } from "../i18n.config";
import { onI18nError, getMessageFallback } from "../infrastructure/i18n";

import enMessages from "../locales/en.json";
import filMessages from "../locales/fil.json";

const messagesByLocale: Record<Locale, typeof enMessages> = {
  en: enMessages,
  fil: filMessages,
};

/**
 * Client-side i18n provider for BiyaHero.
 *
 * - Loads messages based on the user's language preference
 * - Detects locale from localStorage or navigator.language
 * - Defaults to "fil" (Filipino-first per requirements)
 * - Falls back to English for missing keys and reports them
 *
 * Requirements: 10.1, 10.2, 10.6, 10.7, 10.8
 */
export default function I18nProvider({ children }: { children: React.ReactNode }) {
  const [locale, setLocale] = useState<Locale>(i18nConfig.defaultLocale);

  useEffect(() => {
    const detected = detectLocale();
    setLocale(detected);

    // Listen for locale changes from the language-preference store
    const handleStorageChange = (e: StorageEvent) => {
      if (e.key === "biyahero-locale" && (e.newValue === "en" || e.newValue === "fil")) {
        setLocale(e.newValue);
      }
    };

    // Also listen for custom events dispatched by the language-preference store
    const handleLocaleChange = (e: Event) => {
      const detail = (e as CustomEvent<{ locale: Locale }>).detail;
      if (detail?.locale) {
        setLocale(detail.locale);
      }
    };

    window.addEventListener("storage", handleStorageChange);
    window.addEventListener("biyahero-locale-change", handleLocaleChange);

    return () => {
      window.removeEventListener("storage", handleStorageChange);
      window.removeEventListener("biyahero-locale-change", handleLocaleChange);
    };
  }, []);

  const messages = messagesByLocale[locale];

  return (
    <NextIntlClientProvider
      locale={locale}
      messages={messages}
      onError={onI18nError}
      getMessageFallback={getMessageFallback}
    >
      {children}
    </NextIntlClientProvider>
  );
}
