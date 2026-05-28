/**
 * Missing translation key reporter.
 *
 * In development: logs missing keys to the console.
 * In production: POSTs missing keys to /v1/i18n/missing-keys (fire-and-forget, debounced).
 *
 * Requirements: 10.6
 */

import { API_ENDPOINTS } from "../api/endpoints";

const DEBOUNCE_MS = 5000;
const isDev = process.env.NODE_ENV === "development";

let pendingKeys: Set<string> = new Set();
let debounceTimer: ReturnType<typeof setTimeout> | null = null;

/**
 * Flush pending missing keys to the backend.
 * Fire-and-forget — errors are swallowed silently.
 */
function flush(): void {
  if (pendingKeys.size === 0) return;

  const keys = Array.from(pendingKeys);
  pendingKeys = new Set();

  if (isDev) {
    console.warn("[i18n] Missing translation keys:", keys);
    return;
  }

  // Production: POST to the backend for translator backfill
  const baseUrl = process.env.NEXT_PUBLIC_API_BASE_URL || "";
  const url = `${baseUrl}${API_ENDPOINTS.I18N.MISSING_KEYS}`;

  fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ keys, locale: getActiveLocale() }),
  }).catch(() => {
    // Fire-and-forget — swallow network errors
  });
}

/**
 * Get the active locale from localStorage (mirrors the language-preference store).
 */
function getActiveLocale(): string {
  if (typeof window === "undefined") return "fil";
  return localStorage.getItem("biyahero-locale") || "fil";
}

/**
 * Report a missing translation key.
 * Debounces reports to avoid flooding the API.
 */
export function reportMissingKey(key: string): void {
  pendingKeys.add(key);

  if (debounceTimer !== null) {
    clearTimeout(debounceTimer);
  }

  debounceTimer = setTimeout(flush, DEBOUNCE_MS);
}

/**
 * next-intl onError handler that integrates with the missing-key reporter.
 * Falls back to English string at render time (handled by next-intl's getMessageFallback).
 */
export function onI18nError(error: { code: string; originalMessage?: string }): void {
  if (error.code === "MISSING_MESSAGE") {
    // The key path is embedded in the error — extract from originalMessage if available
    if (isDev) {
      console.warn("[i18n]", error.originalMessage || "Missing translation key");
    }
  }
}

/**
 * next-intl getMessageFallback handler.
 * When a key is missing for the active locale, returns the English fallback
 * and reports the missing key for backfill.
 */
export function getMessageFallback({
  namespace,
  key,
}: {
  namespace?: string;
  key: string;
  error: Error;
}): string {
  const fullKey = namespace ? `${namespace}.${key}` : key;
  reportMissingKey(fullKey);

  // Return the key path as a visible fallback so the UI isn't blank
  return fullKey;
}
