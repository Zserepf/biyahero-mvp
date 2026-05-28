'use client';

/**
 * CommuterHeatmapPage — Page for commuters to signal "I'm waiting here."
 *
 * Displays a map centered on the commuter's current location (or Philippines center
 * as fallback). Provides a button to submit a demand-ping and a cancel button to
 * remove the active ping. WebSocket connects on mount and disconnects on unmount.
 *
 * Requirements: 4.1, 4.5, 9.1 (44×44px hit targets), 9.5 (accessible names)
 */

import { useState, useEffect, useCallback } from 'react';
import { MapContainer, TileLayer, Marker, Circle, useMap } from 'react-leaflet';
import L from 'leaflet';
import Link from 'next/link';
import { useCommuterHeatmap } from './useCommuterHeatmap';
import type { VehicleType } from './types';

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

// ─── Constants ───────────────────────────────────────────────────────────────

const PH_CENTER: [number, number] = [14.5995, 120.9842]; // Metro Manila default
const PH_ZOOM = 15;

const VEHICLE_OPTIONS: { value: VehicleType; label: string }[] = [
  { value: 'jeepney', label: 'Jeepney' },
  { value: 'uv_express', label: 'UV Express' },
  { value: 'bus', label: 'Bus' },
];

// ─── Map Center Updater ──────────────────────────────────────────────────────

function MapCenterUpdater({ center }: { center: [number, number] }) {
  const map = useMap();

  useEffect(() => {
    map.setView(center, map.getZoom());
  }, [center, map]);

  return null;
}

// ─── Connection Status Badge ─────────────────────────────────────────────────

function ConnectionBadge({ status }: { status: string }) {
  const statusConfig: Record<string, { color: string; text: string }> = {
    connected: { color: 'bg-green-500', text: 'Connected' },
    connecting: { color: 'bg-yellow-500', text: 'Connecting...' },
    disconnected: { color: 'bg-gray-400', text: 'Disconnected' },
    error: { color: 'bg-red-500', text: 'Error' },
  };

  const config = statusConfig[status] || statusConfig.disconnected;

  return (
    <div className="flex items-center gap-2" aria-live="polite">
      <span
        className={`inline-block h-2.5 w-2.5 rounded-full ${config.color}`}
        aria-hidden="true"
      />
      <span className="text-xs font-medium text-gray-600 dark:text-white/70">{config.text}</span>
    </div>
  );
}

// ─── Main Page Component ─────────────────────────────────────────────────────

export function CommuterHeatmapPage() {
  const { status, activePing, submitDemandPing, cancelDemand, error } =
    useCommuterHeatmap();

  const [userLocation, setUserLocation] = useState<[number, number]>(PH_CENTER);
  const [vehicleType, setVehicleType] = useState<VehicleType>('jeepney');
  const [locationError, setLocationError] = useState<string | null>(null);
  const [geoLoading, setGeoLoading] = useState(true);

  // ─── Get user's current location ──────────────────────────────────────

  useEffect(() => {
    if (!navigator.geolocation) {
      setLocationError('Geolocation is not supported by your browser.');
      setGeoLoading(false);
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        setUserLocation([position.coords.latitude, position.coords.longitude]);
        setGeoLoading(false);
      },
      (err) => {
        setLocationError(
          err.code === err.PERMISSION_DENIED
            ? 'Location access denied. Using default location.'
            : 'Unable to get your location. Using default location.',
        );
        setGeoLoading(false);
      },
      { enableHighAccuracy: true, timeout: 10000, maximumAge: 30000 },
    );
  }, []);

  // ─── Handlers ──────────────────────────────────────────────────────────

  const handleSubmitPing = useCallback(() => {
    submitDemandPing({
      lat: userLocation[0],
      lng: userLocation[1],
      vehicleType,
    });
  }, [submitDemandPing, userLocation, vehicleType]);

  const handleCancel = useCallback(() => {
    cancelDemand();
  }, [cancelDemand]);

  // ─── Render ────────────────────────────────────────────────────────────

  return (
    <main className="flex h-full flex-col bg-gray-50 dark:bg-slate-900">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-black/10 dark:border-white/10 bg-white/80 dark:bg-slate-900/80 px-4 py-3 backdrop-blur-md">
        <div className="flex items-center gap-3">
          <Link
            href="/"
            className="flex h-9 w-9 items-center justify-center rounded-xl bg-gray-100 dark:bg-white/10 text-gray-700 dark:text-white transition hover:bg-gray-200 dark:hover:bg-white/20"
            aria-label="Back to home"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
            </svg>
          </Link>
          <h1 className="text-base font-bold text-gray-900 dark:text-white">Waiting for a Ride</h1>
        </div>
        <ConnectionBadge status={status} />
      </div>

      {/* Error messages */}
      {(error || locationError) && (
        <div
          className="mx-4 mt-3 rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-600 dark:text-red-400"
          role="alert"
          aria-live="assertive"
        >
          {error || locationError}
        </div>
      )}

      {/* Map */}
      <div
        className="relative flex-1"
        role="application"
        aria-label="Commuter location map"
      >
        {geoLoading ? (
          <div className="flex h-full items-center justify-center bg-gray-50 dark:bg-slate-900">
            <div className="flex flex-col items-center gap-3">
              <span className="h-6 w-6 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
              <p className="text-sm text-gray-400 dark:text-white/40">Getting your location…</p>
            </div>
          </div>
        ) : (
          <MapContainer
            center={userLocation}
            zoom={PH_ZOOM}
            style={{ height: '100%', width: '100%' }}
            scrollWheelZoom={true}
          >
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <MapCenterUpdater center={userLocation} />

            {/* User location marker */}
            <Marker
              position={userLocation}
              title="Your location"
            />

            {/* Active ping radius indicator */}
            {activePing && (
              <Circle
                center={[activePing.lat, activePing.lng]}
                radius={150}
                pathOptions={{
                  color: '#f59e0b',
                  fillColor: '#fbbf24',
                  fillOpacity: 0.3,
                  weight: 2,
                }}
              />
            )}
          </MapContainer>
        )}
      </div>

      {/* Controls */}
      <div className="border-t border-black/10 dark:border-white/10 bg-white dark:bg-slate-900 px-4 py-4">
        {/* Vehicle type selector */}
        <div className="mb-3">
          <label
            htmlFor="vehicle-type-select"
            className="mb-1.5 block text-sm font-medium text-gray-600 dark:text-white/70"
          >
            Vehicle Type
          </label>
          <select
            id="vehicle-type-select"
            value={vehicleType}
            onChange={(e) => setVehicleType(e.target.value as VehicleType)}
            disabled={!!activePing}
            className="min-h-[44px] w-full rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-slate-800 px-4 py-2.5 text-sm text-gray-900 dark:text-white focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 disabled:opacity-40 transition"
            aria-label="Select vehicle type"
          >
            {VEHICLE_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* Action buttons */}
        {!activePing ? (
          <button
            onClick={handleSubmitPing}
            disabled={status !== 'connected'}
            className="min-h-[44px] w-full rounded-xl bg-amber-500 px-4 py-3 text-base font-semibold text-white shadow-lg shadow-amber-500/20 transition hover:bg-amber-400 focus:outline-none focus:ring-2 focus:ring-amber-400/50 disabled:cursor-not-allowed disabled:opacity-40"
            aria-label="Signal that you are waiting for a ride here"
          >
            I&apos;m Waiting Here
          </button>
        ) : (
          <div className="space-y-2">
            <div
              className="rounded-xl border border-amber-500/20 bg-amber-500/10 p-3 text-center"
              role="status"
              aria-live="polite"
            >
              <p className="text-sm font-semibold text-amber-500 dark:text-amber-400">Your ping is active!</p>
              <p className="mt-0.5 text-xs text-amber-500/70 dark:text-amber-400/70">
                Nearby drivers can see your demand signal.
              </p>
            </div>
            <button
              onClick={handleCancel}
              disabled={status !== 'connected'}
              className="min-h-[44px] w-full rounded-xl border border-red-500/40 bg-red-500/10 px-4 py-3 text-base font-semibold text-red-500 dark:text-red-400 transition hover:bg-red-500/20 focus:outline-none focus:ring-2 focus:ring-red-400/30 disabled:cursor-not-allowed disabled:opacity-40"
              aria-label="Cancel your waiting signal"
            >
              Cancel Waiting
            </button>
          </div>
        )}
      </div>
    </main>
  );
}
