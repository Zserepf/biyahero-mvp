'use client';

/**
 * DriverHeatmapMap — Embedded full-screen heatmap for the Driver Dashboard.
 * Reuses the existing useDriverHeatmap hook and HeatmapTileOverlay.
 * Strips the standalone page chrome (header, legend) — those live in DriverDashboard.
 */

import { useCallback, useMemo } from 'react';
import { MapContainer, TileLayer, useMap, useMapEvents } from 'react-leaflet';
import L from 'leaflet';
import { useDriverHeatmap } from '@/features/heatmap/driver-heatmap/useDriverHeatmap';
import { HeatmapTileOverlay } from '@/features/heatmap/driver-heatmap/HeatmapTileOverlay';
import type { Bbox } from '@/features/heatmap/driver-heatmap/types';

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
const PH_ZOOM = 14;

function BoundsSubscriber({ onBoundsChange }: { onBoundsChange: (b: Bbox) => void }) {
  const map = useMap();
  const emit = useCallback(() => {
    const b = map.getBounds();
    onBoundsChange({
      swLat: b.getSouthWest().lat,
      swLng: b.getSouthWest().lng,
      neLat: b.getNorthEast().lat,
      neLng: b.getNorthEast().lng,
    });
  }, [map, onBoundsChange]);
  useMapEvents({ moveend: emit, zoomend: emit });
  return null;
}

function InitialBoundsEmitter({ onBoundsChange }: { onBoundsChange: (b: Bbox) => void }) {
  const map = useMap();
  useMemo(() => {
    setTimeout(() => {
      const b = map.getBounds();
      onBoundsChange({
        swLat: b.getSouthWest().lat,
        swLng: b.getSouthWest().lng,
        neLat: b.getNorthEast().lat,
        neLng: b.getNorthEast().lng,
      });
    }, 150);
  }, [map, onBoundsChange]);
  return null;
}

// Nav bar height — keep in sync with the bottom nav in DriverDashboard
const NAV_HEIGHT = 64;

// Demand legend overlay — sits above the bottom nav
function DemandLegend({ tiles }: { tiles: { demandCount: number }[] }) {
  const total = tiles.reduce((s, t) => s + t.demandCount, 0);
  return (
    // bottom offset = nav height + 8px gap so it's never hidden
    <div
      className="absolute left-4 z-[500] rounded-2xl bg-slate-900/90 p-3 backdrop-blur-sm ring-1 ring-white/10 shadow-xl"
      style={{ bottom: NAV_HEIGHT + 8 }}
    >
      {total > 0 ? (
        <p className="text-xs font-semibold text-white">
          🔴 {total} commuter{total !== 1 ? 's' : ''} waiting
        </p>
      ) : (
        <p className="text-xs text-white/40">No demand in view</p>
      )}
      <div className="mt-2 flex flex-col gap-1">
        {[
          { color: '#ef4444', label: 'Very High' },
          { color: '#f97316', label: 'High' },
          { color: '#f59e0b', label: 'Moderate' },
          { color: '#eab308', label: 'Low' },
          { color: '#84cc16', label: 'Minimal' },
        ].map((item) => (
          <div key={item.label} className="flex items-center gap-2">
            <span className="h-2.5 w-2.5 rounded-sm flex-shrink-0" style={{ backgroundColor: item.color }} />
            <span className="text-[10px] text-white/60">{item.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

export function DriverHeatmapMap() {
  const { tiles, subscribeToBbox } = useDriverHeatmap({ enabled: true });
  const handleBoundsChange = useCallback((bbox: Bbox) => subscribeToBbox(bbox), [subscribeToBbox]);

  return (
    <div className="relative h-full w-full">
      <MapContainer
        center={PH_CENTER}
        zoom={PH_ZOOM}
        // paddingBottomRight pushes Leaflet's usable viewport up by the nav height
        // so tiles, attribution, and zoom controls are never hidden behind the nav
        style={{ height: '100%', width: '100%', paddingBottom: NAV_HEIGHT }}
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
      <DemandLegend tiles={tiles} />
    </div>
  );
}
