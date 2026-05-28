"use client";

/**
 * ThemeProvider — applies the resolved theme class to <html> and keeps it
 * in sync with the Zustand store + OS preference changes.
 *
 * Must wrap the app in layout.tsx. The <html> tag needs suppressHydrationWarning
 * to avoid a mismatch between SSR (no class) and client (dark/light class).
 */

import { useEffect } from "react";
import { useThemePreferenceStore } from "../infrastructure/stores/theme-preference-store";

export default function ThemeProvider({
  children,
}: {
  children: React.ReactNode;
}) {
  const { theme, resolvedTheme, setTheme } = useThemePreferenceStore();

  // Apply / remove the "dark" class on <html> whenever the resolved theme changes
  useEffect(() => {
    const root = document.documentElement;
    if (resolvedTheme === "dark") {
      root.classList.add("dark");
    } else {
      root.classList.remove("dark");
    }
  }, [resolvedTheme]);

  // When theme is "system", listen for OS preference changes
  useEffect(() => {
    if (theme !== "system") return;

    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    const handler = () => {
      // Re-trigger resolution by re-setting to "system"
      setTheme("system");
    };

    mq.addEventListener("change", handler);
    return () => mq.removeEventListener("change", handler);
  }, [theme, setTheme]);

  return <>{children}</>;
}
