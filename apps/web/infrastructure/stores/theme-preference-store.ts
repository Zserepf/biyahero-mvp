/**
 * Theme-preference Zustand store with localStorage mirror.
 *
 * - Stores the current theme: "light" | "dark" | "system"
 * - Mirrors to localStorage on every change (offline-resilient)
 * - Dispatches a custom event so ThemeProvider picks up changes immediately
 * - "system" respects the OS prefers-color-scheme media query
 *
 * Follows the same pattern as language-preference-store.ts.
 */

import { create } from "zustand";

export type Theme = "light" | "dark" | "system";

const THEME_STORAGE_KEY = "biyahero-theme";
const THEME_CHANGE_EVENT = "biyahero-theme-change";

// ─── Types ───────────────────────────────────────────────────────────────────

interface ThemePreferenceState {
  theme: Theme;
  setTheme: (theme: Theme) => void;
  /** Resolved theme — "light" or "dark" after applying system preference */
  resolvedTheme: "light" | "dark";
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function readStoredTheme(): Theme {
  if (typeof window === "undefined") return "dark";
  const stored = localStorage.getItem(THEME_STORAGE_KEY);
  if (stored === "light" || stored === "dark" || stored === "system") {
    return stored;
  }
  return "dark"; // default to dark (app was dark-first)
}

function resolveTheme(theme: Theme): "light" | "dark" {
  if (theme !== "system") return theme;
  if (typeof window === "undefined") return "dark";
  return window.matchMedia("(prefers-color-scheme: dark)").matches
    ? "dark"
    : "light";
}

function persistToLocalStorage(theme: Theme): void {
  if (typeof window === "undefined") return;
  localStorage.setItem(THEME_STORAGE_KEY, theme);
}

function dispatchThemeChangeEvent(theme: Theme): void {
  if (typeof window === "undefined") return;
  window.dispatchEvent(
    new CustomEvent(THEME_CHANGE_EVENT, { detail: { theme } })
  );
}

// ─── Store ───────────────────────────────────────────────────────────────────

const initialTheme = readStoredTheme();

export const useThemePreferenceStore = create<ThemePreferenceState>(
  (set, get) => ({
    theme: initialTheme,
    resolvedTheme: resolveTheme(initialTheme),

    setTheme: (theme: Theme) => {
      if (theme === get().theme) return;
      const resolved = resolveTheme(theme);
      set({ theme, resolvedTheme: resolved });
      persistToLocalStorage(theme);
      dispatchThemeChangeEvent(theme);
    },
  })
);
