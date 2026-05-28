/**
 * Routes layout — prevents static generation for map-based pages.
 * Leaflet requires browser APIs (window, document) that aren't available during SSG.
 */

export const dynamic = 'force-dynamic';

export default function RoutesLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
