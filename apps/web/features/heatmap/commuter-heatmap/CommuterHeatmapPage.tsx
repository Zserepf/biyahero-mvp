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
        className={`inline-block h-3 w-3 rounded-full ${config.color}`}
        aria-hidden="true"
      />
      <span className="text-xs font-medium text-gray-700">{config.text}</span>
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
    <main className="flex h-full flex-col">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-gray-200 px-4 py-3">
        <h1 className="text-lg font-bold text-gray-900">Waiting for a Ride</h1>
        <ConnectionBadge status={status} />
      </div>

      {/* Error messages */}
      {(error || locationError) && (
        <div
          className="mx-4 mt-3 rounded-lg bg-red-50 p-3 text-sm text-red-700"
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
          <div className="flex h-full items-center justify-center">
            <p className="text-sm text-gray-500">Getting your location...</p>
          </div>
        ) : (
          <MapContainer
            center={userLocation}
            zoom={PH_ZOOM}
            className="h-full w-full"
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
      <div className="border-t border-gray-200 bg-white px-4 py-4">
        {/* Vehicle type selector */}
        <div className="mb-3">
          <label
            htmlFor="vehicle-type-select"
            className="mb-1 block text-sm font-medium text-gray-700"
          >
            Vehicle Type
          </label>
          <select
            id="vehicle-type-select"
            value={vehicleType}
            onChange={(e) => setVehicleType(e.target.value as VehicleType)}
            disabled={!!activePing}
            className="min-h-[44px] w-full rounded-lg border border-gray-300 px-3 py-2 text-base focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-300 disabled:bg-gray-100 disabled:text-gray-500"
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
            className="min-h-[44px] w-full rounded-lg bg-amber-500 px-4 py-3 text-base font-semibold text-white shadow-sm hover:bg-amber-600 focus:outline-none focus:ring-2 focus:ring-amber-300 disabled:cursor-not-allowed disabled:bg-gray-300 disabled:text-gray-500"
            aria-label="Signal that you are waiting for a ride here"
          >
            I&apos;m Waiting Here
          </button>
        ) : (
          <div className="space-y-2">
            <div
              className="rounded-lg bg-amber-50 p-3 text-center text-sm text-amber-800"
              role="status"
              aria-live="polite"
            >
              <p className="font-medium">Your ping is active!</p>
              <p className="text-xs text-amber-600">
                Nearby drivers can see your demand signal.
              </p>
            </div>
            <button
              onClick={handleCancel}
              disabled={status !== 'connected'}
              className="min-h-[44px] w-full rounded-lg border-2 border-red-500 bg-white px-4 py-3 text-base font-semibold text-red-600 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-300 disabled:cursor-not-allowed disabled:border-gray-300 disabled:text-gray-500"
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
