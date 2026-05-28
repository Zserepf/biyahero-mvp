import type { Metadata, Viewport } from "next";
import "./globals.css";
import SwUpdateBanner from "../shared/components/SwUpdateBanner";
import NetworkStatus from "../shared/components/NetworkStatus";
import I18nProvider from "../components/I18nProvider";
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
  themeColor: "#2563eb",
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="fil">
      <body className="min-h-screen bg-white text-gray-900 antialiased">
        <QueryProvider>
          <I18nProvider>
            <NetworkStatus />
            {children}
            <SwUpdateBanner />
          </I18nProvider>
        </QueryProvider>
      </body>
    </html>
  );
}
