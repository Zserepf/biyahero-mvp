'use client';

/**
 * FareMapPicker — Interactive Leaflet map for selecting origin/destination.
 *
 * First click sets origin (green marker), second click sets destination (red marker).
 * Must be loaded with ssr: false via next/dynamic.
 *
 * Requirements: 2.1
 */

import { useState, useCallback } from 'react';
import { MapContainer, TileLayer, Marker, Popup, useMapEvents } from 'react-leaflet';
import L from 'leaflet';
import type { Coordinate } from './types';

// Fix default marker icon issue with webpack/next.js
delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

// Custom icons for origin (green) and destination (red)
const originIcon = new L.Icon({
  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-green.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
});

const destinationIcon = new L.Icon({
  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-red.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
});

interface FareMapPickerProps {
  origin: Coordinate | null;
  destination: Coordinate | null;
  onOriginChange: (coord: Coordinate) => void;
  onDestinationChange: (coord: Coordinate) => void;
}

function MapClickHandler({
  origin,
  onOriginChange,
  onDestinationChange,
}: {
  origin: Coordinate | null;
  onOriginChange: (coord: Coordinate) => void;
  onDestinationChange: (coord: Coordinate) => void;
}) {
  useMapEvents({
    click(e) {
      const coord: Coordinate = { lat: e.latlng.lat, lng: e.latlng.lng };
      if (!origin) {
        onOriginChange(coord);
      } else {
        onDestinationChange(coord);
      }
    },
  });
  return null;
}

export function FareMapPicker({
  origin,
  destination,
  onOriginChange,
  onDestinationChange,
}: FareMapPickerProps) {
  return (
    <div className="relative w-full overflow-hidden rounded-xl border border-white/10">
      <MapContainer
        center={[14.5995, 120.9842]}
        zoom={12}
        style={{ height: '360px', width: '100%' }}
        className="z-0"
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <MapClickHandler
          origin={origin}
          onOriginChange={onOriginChange}
          onDestinationChange={onDestinationChange}
        />
        {origin && (
          <Marker position={[origin.lat, origin.lng]} icon={originIcon}>
            <Popup>
              <span className="font-medium text-green-700">Origin</span>
              <br />
              {origin.lat.toFixed(5)}, {origin.lng.toFixed(5)}
            </Popup>
          </Marker>
        )}
        {destination && (
          <Marker position={[destination.lat, destination.lng]} icon={destinationIcon}>
            <Popup>
              <span className="font-medium text-red-700">Destination</span>
              <br />
              {destination.lat.toFixed(5)}, {destination.lng.toFixed(5)}
            </Popup>
          </Marker>
        )}
      </MapContainer>
    </div>
  );
}
