'use client';

/**
 * DriverHeatmapPage — Map view showing demand tiles as colored overlays.
 *
 * - Subscribes to heatmap via WebSocket with the current map bbox
 * - Re-subscribes on pan/zoom
 * - Renders geohash7 demand tiles in real time
 * - NEVER displays commuter identity (no names, IDs, or personal info)
 * - Only shows: geohash7 location, demand count, vehicle type
 *
 * Requirements: 4.2, 4.3, 4.6
 */

import { useCallback, useMemo } from 'react';
import { MapContainer, TileLayer, useMap, useMapEvents } from 'react-leaflet';
import L from 'leaflet';
import { useDriverHeatmap } from './useDriverHeatmap';
import { HeatmapTileOverlay } from './HeatmapTileOverlay';
import type { Bbox } from './types';

// Fix Leaflet default marker icon issue with bundlers
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

// ─── Philippines center coordinates ──────────────────────────────────────────

const PH_CENTER: [number, number] = [14.5995, 120.9842]; // Metro Manila
const PH_ZOOM = 13; // City-level zoom for driver view

// ─── Connection Status Badge ─────────────────────────────────────────────────

function ConnectionStatusBadge({ status }: { status: string }) {
  const statusConfig: Record<string, { label: string; className: string }> = {
    connected: {
      label: 'Live',
      className: 'bg-green-600 text-white',
    },
    connecting: {
      label: 'Connecting...',
      className: 'bg-yellow-500 text-black',
    },
    disconnected: {
      label: 'Disconnected',
      className: 'bg-gray-500 text-white',
    },
    error: {
      label: 'Error',
      className: 'bg-red-600 text-white',
    },
  };

  const config = statusConfig[status] || statusConfig.disconnected;

  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold ${config.className}`}
      role="status"
      aria-live="polite"
    >
      {status === 'connected' && (
        <span className="h-2 w-2 rounded-full bg-white animate-pulse" aria-hidden="true" />
      )}
      {config.label}
    </span>
  );
}

// ─── Map Bounds Handler ──────────────────────────────────────────────────────

function BoundsSubscriber({
  onBoundsChange,
}: {
  onBoundsChange: (bbox: Bbox) => void;
}) {
  const map = useMap();

  const emitBounds = useCallback(() => {
    const bounds = map.getBounds();
    onBoundsChange({
      swLat: bounds.getSouthWest().lat,
      swLng: bounds.getSouthWest().lng,
      neLat: bounds.getNorthEast().lat,
      neLng: bounds.getNorthEast().lng,
    });
  }, [map, onBoundsChange]);

  useMapEvents({
    moveend: emitBounds,
    zoomend: emitBounds,
    load: emitBounds,
  });

  return null;
}

// ─── Initial Bounds Emitter ──────────────────────────────────────────────────

function InitialBoundsEmitter({
  onBoundsChange,
}: {
  onBoundsChange: (bbox: Bbox) => void;
}) {
  const map = useMap();

  // Emit initial bounds once the map is ready
  useMemo(() => {
    // Use setTimeout to ensure the map has rendered
    setTimeout(() => {
      const bounds = map.getBounds();
      onBoundsChange({
        swLat: bounds.getSouthWest().lat,
        swLng: bounds.getSouthWest().lng,
        neLat: bounds.getNorthEast().lat,
        neLng: bounds.getNorthEast().lng,
      });
    }, 100);
  }, [map, onBoundsChange]);

  return null;
}

// ─── Main Page Component ─────────────────────────────────────────────────────

export function DriverHeatmapPage() {
  const { tiles, status, subscribeToBbox } = useDriverHeatmap({ enabled: true });

  const handleBoundsChange = useCallback(
    (bbox: Bbox) => {
      subscribeToBbox(bbox);
    },
    [subscribeToBbox],
  );

  const totalDemand = tiles.reduce((sum, tile) => sum + tile.demandCount, 0);

  return (
    <div className="flex h-full flex-col">
      {/* Header */}
      <header className="flex items-center justify-between border-b border-gray-200 bg-white px-4 py-3">
        <div>
          <h1 className="text-lg font-bold text-gray-900">Demand Heatmap</h1>
          <p className="text-sm text-gray-500">
            {tiles.length > 0
              ? `${totalDemand} commuter${totalDemand !== 1 ? 's' : ''} waiting in ${tiles.length} area${tiles.length !== 1 ? 's' : ''}`
              : 'No active demand in this area'}
          </p>
        </div>
        <ConnectionStatusBadge status={status} />
      </header>

      {/* Map */}
      <div
        className="relative flex-1"
        role="application"
        aria-label="Driver demand heatmap"
      >
        <MapContainer
          center={PH_CENTER}
          zoom={PH_ZOOM}
          className="h-full w-full"
          scrollWheelZoom={true}
        >
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />

          {/* Subscribe to bbox changes on pan/zoom */}
          <BoundsSubscriber onBoundsChange={handleBoundsChange} />
          <InitialBoundsEmitter onBoundsChange={handleBoundsChange} />

          {/* Render demand tiles as colored overlays */}
          {tiles.map((tile) => (
            <HeatmapTileOverlay key={tile.geohash7} tile={tile} />
          ))}
        </MapContainer>

        {/* Legend overlay */}
        <div
          className="absolute bottom-4 left-4 z-[1000] rounded-lg bg-white/90 p-3 shadow-md backdrop-blur-sm"
          aria-label="Demand legend"
        >
          <p className="mb-2 text-xs font-semibold text-gray-700">Demand Level</p>
          <div className="flex flex-col gap-1">
            <LegendItem color="#dc2626" label="Very High (10+)" />
            <LegendItem color="#ea580c" label="High (7-9)" />
            <LegendItem color="#d97706" label="Moderate (4-6)" />
            <LegendItem color="#ca8a04" label="Low (2-3)" />
            <LegendItem color="#65a30d" label="Minimal (1)" />
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── Legend Item ──────────────────────────────────────────────────────────────

function LegendItem({ color, label }: { color: string; label: string }) {
  return (
    <div className="flex items-center gap-2">
      <span
        className="inline-block h-3 w-3 rounded-sm"
        style={{ backgroundColor: color }}
        aria-hidden="true"
      />
      <span className="text-xs text-gray-600">{label}</span>
    </div>
  );
}
