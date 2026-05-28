'use client';

/**
 * HeatmapTileOverlay — Renders a single geohash7 tile on the map
 * with a demand count indicator.
 *
 * Displays a colored rectangle representing the geohash7 cell area,
 * with color intensity based on demand count. Shows demand count as a label.
 *
 * NEVER displays commuter identity — only geohash7, demand count, vehicle type.
 *
 * Requirements: 4.2, 4.3, 4.6
 */

import { Rectangle, Tooltip } from 'react-leaflet';
import type { LatLngBoundsExpression } from 'leaflet';
import type { HeatmapTile } from './types';

interface HeatmapTileOverlayProps {
  tile: HeatmapTile;
}

/**
 * Decode a geohash string into approximate lat/lng bounds.
 * Geohash precision 7 gives ~150m × 150m cells.
 */
function decodeGeohashBounds(geohash: string): LatLngBoundsExpression {
  const BASE32 = '0123456789bcdefghjkmnpqrstuvwxyz';

  let latMin = -90;
  let latMax = 90;
  let lngMin = -180;
  let lngMax = 180;
  let isLng = true;

  for (const char of geohash) {
    const idx = BASE32.indexOf(char);
    if (idx === -1) continue;

    for (let bit = 4; bit >= 0; bit--) {
      const bitValue = (idx >> bit) & 1;

      if (isLng) {
        const mid = (lngMin + lngMax) / 2;
        if (bitValue === 1) {
          lngMin = mid;
        } else {
          lngMax = mid;
        }
      } else {
        const mid = (latMin + latMax) / 2;
        if (bitValue === 1) {
          latMin = mid;
        } else {
          latMax = mid;
        }
      }

      isLng = !isLng;
    }
  }

  return [
    [latMin, lngMin],
    [latMax, lngMax],
  ];
}

/**
 * Get the fill color based on demand count intensity.
 * Higher demand = warmer (more red) color.
 */
function getDemandColor(demandCount: number): string {
  if (demandCount >= 10) return '#dc2626'; // red-600 — very high demand
  if (demandCount >= 7) return '#ea580c'; // orange-600 — high demand
  if (demandCount >= 4) return '#d97706'; // amber-600 — moderate demand
  if (demandCount >= 2) return '#ca8a04'; // yellow-600 — low demand
  return '#65a30d'; // lime-600 — minimal demand
}

/**
 * Get fill opacity based on demand count.
 * Higher demand = more opaque.
 */
function getDemandOpacity(demandCount: number): number {
  if (demandCount >= 10) return 0.6;
  if (demandCount >= 7) return 0.5;
  if (demandCount >= 4) return 0.4;
  if (demandCount >= 2) return 0.35;
  return 0.3;
}

export function HeatmapTileOverlay({ tile }: HeatmapTileOverlayProps) {
  const bounds = decodeGeohashBounds(tile.geohash7);
  const color = getDemandColor(tile.demandCount);
  const opacity = getDemandOpacity(tile.demandCount);

  return (
    <Rectangle
      bounds={bounds}
      pathOptions={{
        color,
        fillColor: color,
        fillOpacity: opacity,
        weight: 1,
        opacity: 0.7,
      }}
    >
      <Tooltip direction="top" sticky>
        <span className="text-sm font-medium">
          {tile.demandCount} waiting • {tile.vehicleType.replace('_', ' ')}
        </span>
      </Tooltip>
    </Rectangle>
  );
}
