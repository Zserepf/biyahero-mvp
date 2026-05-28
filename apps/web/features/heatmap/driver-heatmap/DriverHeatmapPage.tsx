'use client';

/**
 * DriverHeatmapPage — Real-time demand heatmap for drivers.
 * Requirements: 4.2, 4.3, 4.6
 */

import { useCallback, useMemo } from 'react';
import { MapContainer, TileLayer, useMap, useMapEvents } from 'react-leaflet';
import L from 'leaflet';
import Link from 'next/link';
import { useDriverHeatmap } from './useDriverHeatmap';
import { HeatmapTileOverlay } from './HeatmapTileOverlay';
import type { Bbox } from './types';

const defaultIcon = L.icon({
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
});
L.Marker.prototype.options.icon = defaultIcon;

const PH_CENTER: [number, number] = [14.5995, 120.9842];
const PH_ZOOM = 13;

function StatusDot({ status }: { status: string }) {
  const map: Record<string, { dot: string; label: string; ring: string }> = {
    connected:    { dot: 'bg-emerald-400 animate-pulse', label: 'Live',         ring: 'ring-emerald-400/30' },
    connecting:   { dot: 'bg-yellow-400 animate-pulse',  label: 'Connecting…',  ring: 'ring-yellow-400/30' },
    disconnected: { dot: 'bg-gray-400',                  label: 'Disconnected', ring: 'ring-gray-400/30' },
    error:        { dot: 'bg-red-500',                   label: 'Error',        ring: 'ring-red-500/30' },
  };
  const cfg = map[status] ?? map.disconnected;
  return (
    <span className={`inline-flex items-center gap-2 rounded-full bg-slate-800/80 px-3 py-1.5 text-xs font-semibold text-white ring-1 backdrop-blur-sm ${cfg.ring}`}>
      <span className={`h-2 w-2 rounded-full ${cfg.dot}`} />
      {cfg.label}
    </span>
  );
}

function BoundsSubscriber({ onBoundsChange }: { onBoundsChange: (b: Bbox) => void }) {
  const map = useMap();
  const emit = useCallback(() => {
    const b = map.getBounds();
    onBoundsChange({ swLat: b.getSouthWest().lat, swLng: b.getSouthWest().lng, neLat: b.getNorthEast().lat, neLng: b.getNorthEast().lng });
  }, [map, onBoundsChange]);
  useMapEvents({ moveend: emit, zoomend: emit });
  return null;
}

function InitialBoundsEmitter({ onBoundsChange }: { onBoundsChange: (b: Bbox) => void }) {
  const map = useMap();
  useMemo(() => {
    setTimeout(() => {
      const b = map.getBounds();
      onBoundsChange({ swLat: b.getSouthWest().lat, swLng: b.getSouthWest().lng, neLat: b.getNorthEast().lat, neLng: b.getNorthEast().lng });
    }, 150);
  }, [map, onBoundsChange]);
  return null;
}

export function DriverHeatmapPage() {
  const { tiles, status, subscribeToBbox } = useDriverHeatmap({ enabled: true });
  const handleBoundsChange = useCallback((bbox: Bbox) => subscribeToBbox(bbox), [subscribeToBbox]);
  const totalDemand = tiles.reduce((s, t) => s + t.demandCount, 0);

  return (
    <div className="relative h-screen w-full overflow-hidden bg-gray-100 dark:bg-slate-900">

      {/* ── Top bar ─────────────────────────────────────────────────── */}
      <header className="absolute left-0 right-0 top-0 z-[1001] flex items-center justify-between px-4 py-3">
        {/* Back + title */}
        <div className="flex items-center gap-3">
          <Link
            href="/"
            className="flex h-9 w-9 items-center justify-center rounded-xl bg-white/90 dark:bg-slate-800/80 text-gray-700 dark:text-white backdrop-blur-sm ring-1 ring-black/10 dark:ring-white/10 hover:bg-white dark:hover:bg-slate-700/80 transition"
            aria-label="Back to home"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
            </svg>
          </Link>
          <div className="rounded-xl bg-white/90 dark:bg-slate-800/80 px-4 py-2 backdrop-blur-sm ring-1 ring-black/10 dark:ring-white/10">
            <p className="text-sm font-bold text-gray-900 dark:text-white leading-none">Demand Heatmap</p>
            <p className="mt-0.5 text-xs text-gray-500 dark:text-white/50">
              {tiles.length > 0
                ? `${totalDemand} commuter${totalDemand !== 1 ? 's' : ''} waiting nearby`
                : 'No active demand in view'}
            </p>
          </div>
        </div>
        <StatusDot status={status} />
      </header>

      {/* ── Map — explicit h-screen so Leaflet initialises correctly ── */}
      <div className="h-screen w-full" role="application" aria-label="Driver demand heatmap">
        <MapContainer
          center={PH_CENTER}
          zoom={PH_ZOOM}
          style={{ height: '100%', width: '100%' }}
          scrollWheelZoom
          zoomControl={false}
        >
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <BoundsSubscriber onBoundsChange={handleBoundsChange} />
          <InitialBoundsEmitter onBoundsChange={handleBoundsChange} />
          {tiles.map((tile) => (
            <HeatmapTileOverlay key={tile.geohash7} tile={tile} />
          ))}
        </MapContainer>
      </div>

      {/* ── Legend ──────────────────────────────────────────────────── */}
      <div className="absolute bottom-6 left-4 z-[1001] rounded-2xl bg-white/90 dark:bg-slate-900/90 p-4 shadow-xl backdrop-blur-sm ring-1 ring-black/10 dark:ring-white/10">
        <p className="mb-3 text-[10px] font-bold uppercase tracking-widest text-gray-400 dark:text-white/40">Demand</p>
        <div className="flex flex-col gap-2">
          {[
            { color: '#ef4444', label: 'Very High', sub: '10+' },
            { color: '#f97316', label: 'High',      sub: '7–9' },
            { color: '#f59e0b', label: 'Moderate',  sub: '4–6' },
            { color: '#eab308', label: 'Low',       sub: '2–3' },
            { color: '#84cc16', label: 'Minimal',   sub: '1' },
          ].map((item) => (
            <div key={item.label} className="flex items-center gap-2.5">
              <span className="h-3 w-3 rounded-sm flex-shrink-0" style={{ backgroundColor: item.color }} />
              <span className="text-xs text-gray-700 dark:text-white/70">{item.label}</span>
              <span className="ml-auto text-[10px] text-gray-400 dark:text-white/30">{item.sub}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
