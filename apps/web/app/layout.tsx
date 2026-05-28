import type { Metadata, Viewport } from "next";
import "./globals.css";
import SwUpdateBanner from "../shared/components/SwUpdateBanner";
import NetworkStatus from "../shared/components/NetworkStatus";
import I18nProvider from "../components/I18nProvider";
import ThemeProvider from "../components/ThemeProvider";
import { QueryProvider } from "../infrastructure/query/QueryProvider";

export const metadata: Metadata = {
  title: "BiyaHero",
  description:
    "Community-sourced PUV routing, fare calculator, and real-time payment notifications for Filipino commuters and drivers.",
  manifest: "/manifest.json",
  appleWebApp: {
    capable: true,
    statusBarStyle: "default",
    title: "BiyaHero",
  },
};

export const viewport: Viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#2563eb" },
    { media: "(prefers-color-scheme: dark)", color: "#1d4ed8" },
  ],
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    // suppressHydrationWarning prevents React from complaining about the
    // "dark" class being added client-side by ThemeProvider before hydration.
    <html lang="fil" suppressHydrationWarning>
      <body className="min-h-screen bg-white text-gray-900 antialiased dark:bg-slate-900 dark:text-white">
        <QueryProvider>
          <I18nProvider>
            <ThemeProvider>
              <NetworkStatus />
              {children}
              <SwUpdateBanner />
            </ThemeProvider>
          </I18nProvider>
        </QueryProvider>
      </body>
    </html>
  );
}
