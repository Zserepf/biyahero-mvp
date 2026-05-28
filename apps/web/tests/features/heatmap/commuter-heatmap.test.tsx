/**
 * Component tests for the Commuter Heatmap feature slice.
 *
 * Happy path: demand ping submission when connected.
 * Error path: shows error when not authenticated.
 *
 * Requirements: 4.1 (demand ping submission)
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CommuterHeatmapPage } from '@/features/heatmap/commuter-heatmap/CommuterHeatmapPage';

// ─── WebSocket Mock ──────────────────────────────────────────────────────────

let mockWsInstance: {
  onopen: (() => void) | null;
  onmessage: ((event: { data: string }) => void) | null;
  onerror: (() => void) | null;
  onclose: ((event: { code: number }) => void) | null;
  send: ReturnType<typeof vi.fn>;
  close: ReturnType<typeof vi.fn>;
  readyState: number;
};

class MockWebSocket {
  static OPEN = 1;
  static CLOSED = 3;

  onopen: (() => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;
  onerror: (() => void) | null = null;
  onclose: ((event: { code: number }) => void) | null = null;
  send = vi.fn();
  close = vi.fn();
  readyState = MockWebSocket.OPEN;

  constructor() {
    mockWsInstance = this;
    // Simulate connection opening after a tick
    setTimeout(() => {
      this.onopen?.();
    }, 0);
  }
}

// ─── Geolocation Mock ────────────────────────────────────────────────────────

const mockGeolocation = {
  getCurrentPosition: vi.fn((success: (pos: GeolocationPosition) => void) => {
    success({
      coords: {
        latitude: 14.5995,
        longitude: 120.9842,
        accuracy: 10,
        altitude: null,
        altitudeAccuracy: null,
        heading: null,
        speed: null,
      },
      timestamp: Date.now(),
    } as GeolocationPosition);
  }),
  watchPosition: vi.fn(),
  clearWatch: vi.fn(),
};

describe('CommuterHeatmapPage', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal('WebSocket', MockWebSocket);
    Object.defineProperty(navigator, 'geolocation', {
      value: mockGeolocation,
      writable: true,
    });
    // Stub crypto.randomUUID
    vi.stubGlobal('crypto', {
      randomUUID: () => 'test-uuid-1234',
    });
  });

  it('renders the page with title and vehicle type selector', async () => {
    localStorage.setItem('biyahero_access_token', 'mock-token');

    render(<CommuterHeatmapPage />);

    expect(screen.getByText('Waiting for a Ride')).toBeInTheDocument();
    expect(screen.getByLabelText('Select vehicle type')).toBeInTheDocument();
  });

  it('happy path: submits demand ping when connected', async () => {
    const user = userEvent.setup();
    localStorage.setItem('biyahero_access_token', 'mock-token');

    render(<CommuterHeatmapPage />);

    // Wait for WebSocket to connect
    await waitFor(() => {
      expect(screen.getByText('Connected')).toBeInTheDocument();
    });

    // Click the "I'm Waiting Here" button
    const waitButton = screen.getByRole('button', {
      name: 'Signal that you are waiting for a ride here',
    });
    await user.click(waitButton);

    // Verify WebSocket send was called with demand-ping action
    expect(mockWsInstance.send).toHaveBeenCalledWith(
      expect.stringContaining('"action":"demand-ping"'),
    );

    // Verify the sent data includes coordinates and vehicle type
    const sentData = JSON.parse(mockWsInstance.send.mock.calls[0][0]);
    expect(sentData.data.lat).toBe(14.5995);
    expect(sentData.data.lng).toBe(120.9842);
    expect(sentData.data.vehicleType).toBe('jeepney');
  });

  it('error path: shows authentication error when no token', async () => {
    // Don't set access token
    render(<CommuterHeatmapPage />);

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Authentication required',
      );
    });
  });
});
