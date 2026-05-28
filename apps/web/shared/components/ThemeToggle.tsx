"use client";

/**
 * ThemeToggle — cycles through light → dark → system themes.
 *
 * Renders a compact icon button suitable for placement in any header.
 * Uses the Zustand theme store — no props needed.
 */

import { useThemePreferenceStore } from "../../infrastructure/stores/theme-preference-store";
import type { Theme } from "../../infrastructure/stores/theme-preference-store";

const CYCLE: Theme[] = ["light", "dark", "system"];

const ICONS: Record<Theme, React.ReactNode> = {
  light: (
    // Sun icon
    <svg
      className="h-5 w-5"
      fill="none"
      viewBox="0 0 24 24"
      stroke="currentColor"
      strokeWidth={2}
      aria-hidden="true"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364-6.364l-.707.707M6.343 17.657l-.707.707M17.657 17.657l-.707-.707M6.343 6.343l-.707-.707M12 8a4 4 0 100 8 4 4 0 000-8z"
      />
    </svg>
  ),
  dark: (
    // Moon icon
    <svg
      className="h-5 w-5"
      fill="none"
      viewBox="0 0 24 24"
      stroke="currentColor"
      strokeWidth={2}
      aria-hidden="true"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M21 12.79A9 9 0 1111.21 3 7 7 0 0021 12.79z"
      />
    </svg>
  ),
  system: (
    // Monitor icon
    <svg
      className="h-5 w-5"
      fill="none"
      viewBox="0 0 24 24"
      stroke="currentColor"
      strokeWidth={2}
      aria-hidden="true"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
      />
    </svg>
  ),
};

const LABELS: Record<Theme, string> = {
  light: "Switch to dark mode",
  dark: "Switch to system theme",
  system: "Switch to light mode",
};

interface ThemeToggleProps {
  /** Extra CSS classes for the button wrapper */
  className?: string;
}

export function ThemeToggle({ className = "" }: ThemeToggleProps) {
  const { theme, setTheme } = useThemePreferenceStore();

  function handleClick() {
    const currentIndex = CYCLE.indexOf(theme);
    const nextTheme = CYCLE[(currentIndex + 1) % CYCLE.length];
    setTheme(nextTheme);
  }

  return (
    <button
      type="button"
      onClick={handleClick}
      aria-label={LABELS[theme]}
      title={LABELS[theme]}
      className={`flex h-9 w-9 items-center justify-center rounded-xl transition focus:outline-none focus:ring-2 focus:ring-blue-500/50
        bg-gray-100 text-gray-700 hover:bg-gray-200
        dark:bg-white/10 dark:text-white dark:hover:bg-white/20
        ${className}`}
    >
      {ICONS[theme]}
    </button>
  );
}
