'use client';

/**
 * RouteMap — Leaflet map component for displaying and plotting route waypoints.
 *
 * Renders a Leaflet map centered on the Philippines with optional waypoint markers
 * and polyline. Supports click-to-add-waypoint mode for route creation.
 *
 * Requirements: 1.1, 1.2, 9.1 (44×44px hit targets)
 */

import { useEffect, useRef, useCallback } from 'react';
import {
  MapContainer,
  TileLayer,
  Marker,
  Polyline,
  useMapEvents,
  useMap,
} from 'react-leaflet';
import L from 'leaflet';
import type { Waypoint } from '../types';

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

// ─── Types ───────────────────────────────────────────────────────────────────

interface RouteMapProps {
  /** Waypoints to display on the map */
  waypoints?: Waypoint[];
  /** Whether clicking the map adds a waypoint */
  editable?: boolean;
  /** Callback when a new waypoint is added via click */
  onWaypointAdd?: (lat: number, lng: number) => void;
  /** Callback when the map bounds change (for bbox queries) */
  onBoundsChange?: (bounds: {
    swLat: number;
    swLng: number;
    neLat: number;
    neLng: number;
  }) => void;
  /** Map height CSS value */
  height?: string;
  /** Additional CSS class */
  className?: string;
}

// ─── Philippines center coordinates ──────────────────────────────────────────

const PH_CENTER: [number, number] = [12.8797, 121.774];
const PH_ZOOM = 6;

// ─── Map Click Handler ───────────────────────────────────────────────────────

function MapClickHandler({
  onWaypointAdd,
}: {
  onWaypointAdd?: (lat: number, lng: number) => void;
}) {
  useMapEvents({
    click(e) {
      if (onWaypointAdd) {
        onWaypointAdd(e.latlng.lat, e.latlng.lng);
      }
    },
  });
  return null;
}

// ─── Bounds Change Handler ───────────────────────────────────────────────────

function BoundsChangeHandler({
  onBoundsChange,
}: {
  onBoundsChange?: (bounds: {
    swLat: number;
    swLng: number;
    neLat: number;
    neLng: number;
  }) => void;
}) {
  const map = useMap();

  useMapEvents({
    moveend() {
      if (onBoundsChange) {
        const bounds = map.getBounds();
        onBoundsChange({
          swLat: bounds.getSouthWest().lat,
          swLng: bounds.getSouthWest().lng,
          neLat: bounds.getNorthEast().lat,
          neLng: bounds.getNorthEast().lng,
        });
      }
    },
    zoomend() {
      if (onBoundsChange) {
        const bounds = map.getBounds();
        onBoundsChange({
          swLat: bounds.getSouthWest().lat,
          swLng: bounds.getSouthWest().lng,
          neLat: bounds.getNorthEast().lat,
          neLng: bounds.getNorthEast().lng,
        });
      }
    },
  });

  return null;
}

// ─── Auto-fit bounds to waypoints ────────────────────────────────────────────

function FitBounds({ waypoints }: { waypoints: Waypoint[] }) {
  const map = useMap();
  const fitted = useRef(false);

  useEffect(() => {
    if (waypoints.length >= 2 && !fitted.current) {
      const bounds = L.latLngBounds(
        waypoints.map((wp) => [wp.lat, wp.lng] as [number, number]),
      );
      map.fitBounds(bounds, { padding: [50, 50] });
      fitted.current = true;
    }
  }, [waypoints, map]);

  return null;
}

// ─── Main Component ──────────────────────────────────────────────────────────

export function RouteMap({
  waypoints = [],
  editable = false,
  onWaypointAdd,
  onBoundsChange,
  height = '400px',
  className = '',
}: RouteMapProps) {
  const polylinePositions = waypoints
    .sort((a, b) => a.position - b.position)
    .map((wp) => [wp.lat, wp.lng] as [number, number]);

  return (
    <div
      className={`relative w-full rounded-lg overflow-hidden ${className}`}
      style={{ height }}
      role="application"
      aria-label="Route map"
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

        {editable && <MapClickHandler onWaypointAdd={onWaypointAdd} />}
        {onBoundsChange && <BoundsChangeHandler onBoundsChange={onBoundsChange} />}
        {waypoints.length >= 2 && <FitBounds waypoints={waypoints} />}

        {/* Render waypoint markers */}
        {waypoints.map((wp, index) => (
          <Marker
            key={`wp-${wp.position}-${index}`}
            position={[wp.lat, wp.lng]}
            title={wp.name || `Waypoint ${wp.position + 1}`}
          />
        ))}

        {/* Render polyline connecting waypoints */}
        {polylinePositions.length >= 2 && (
          <Polyline
            positions={polylinePositions}
            color="#2563eb"
            weight={4}
            opacity={0.8}
          />
        )}
      </MapContainer>
    </div>
  );
}
