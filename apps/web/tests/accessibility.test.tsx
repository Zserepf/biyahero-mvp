/**
 * axe-core Accessibility Audit — WCAG 2.1 AA compliance tests.
 *
 * Renders representative pages at 320px viewport width and verifies:
 * - No WCAG 2.1 AA violations (contrast, accessible names, etc.)
 * - All interactive elements have accessible names
 * - Focus management supports keyboard-only navigation
 *
 * Validates: Requirements 9.1, 9.2, 9.3, 9.4, 9.5
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe, { type AxeResults } from 'axe-core';

// ─── Helpers ─────────────────────────────────────────────────────────────────

/**
 * Run axe-core on a container and assert no WCAG 2.1 AA violations.
 * Disables the 'region' rule since we're testing components, not full pages.
 */
async function expectNoA11yViolations(container: HTMLElement) {
  const results: AxeResults = await axe.run(container, {
    rules: {
      region: { enabled: false },
    },
    runOnly: {
      type: 'tag',
      values: ['wcag2a', 'wcag2aa', 'wcag21aa'],
    },
  });

  const violations = results.violations;
  if (violations.length > 0) {
    const messages = violations.map(
      (v) =>
        `[${v.id}] ${v.help} (${v.impact})\n` +
        v.nodes.map((n) => `  - ${n.target.join(', ')}\n    ${n.failureSummary}`).join('\n'),
    );
    throw new Error(
      `Expected no WCAG 2.1 AA violations but found ${violations.length}:\n\n${messages.join('\n\n')}`,
    );
  }
}

// ─── Mocks ───────────────────────────────────────────────────────────────────

// Mock next-intl
vi.mock('next-intl', () => ({
  useTranslations: () => (key: string) => key,
}));

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
  usePathname: () => '/',
}));

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock react-leaflet (map components are not DOM-renderable in jsdom)
vi.mock('react-leaflet', () => ({
  MapContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="map-container" role="application" aria-label="Map">{children}</div>
  ),
  TileLayer: () => null,
  Marker: ({ title }: { title?: string }) => (
    <button aria-label={title || 'Map marker'} data-testid="map-marker">marker</button>
  ),
  Circle: () => <div data-testid="map-circle" aria-hidden="true" />,
  useMap: () => ({ setView: vi.fn(), getZoom: () => 15 }),
}));

// Mock leaflet
vi.mock('leaflet', () => ({
  default: {
    icon: () => ({}),
    Marker: { prototype: { options: { icon: null } } },
  },
  icon: () => ({}),
  Marker: { prototype: { options: { icon: null } } },
}));

// Mock RouteMap component (uses react-leaflet internally)
vi.mock('@/features/routes/components/RouteMap', () => ({
  RouteMap: ({ height }: { height?: string }) => (
    <div data-testid="route-map" role="application" aria-label="Route plotting map" style={{ height: height || '300px' }} />
  ),
}));

// Mock useCreateRoute hook
vi.mock('@/features/routes/create-route/useCreateRoute', () => ({
  useCreateRoute: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
  }),
}));

// Mock useAudioConfirmation hook
vi.mock('@/features/payment/driver-dashboard/useAudioConfirmation', () => ({
  useAudioConfirmation: () => ({
    playConfirmation: vi.fn().mockResolvedValue(true),
  }),
}));

// Mock usePaymentListener hook
vi.mock('@/features/payment/driver-dashboard/usePaymentListener', () => ({
  usePaymentListener: () => ({
    payments: [],
    isConnected: true,
    error: null,
  }),
}));

// Mock useCommuterHeatmap hook
vi.mock('@/features/heatmap/commuter-heatmap/useCommuterHeatmap', () => ({
  useCommuterHeatmap: () => ({
    status: 'connected',
    activePing: null,
    submitDemandPing: vi.fn(),
    cancelDemand: vi.fn(),
    error: null,
  }),
}));

// Mock infrastructure
vi.mock('@/infrastructure/api/client', () => ({
  apiClient: { post: vi.fn(), get: vi.fn() },
  ACCESS_TOKEN_KEY: 'biyahero_access_token',
}));

vi.mock('@/infrastructure/api/endpoints', () => ({
  API_ENDPOINTS: {
    AUTH: { LOGIN: '/v1/auth/sessions' },
    FARE: { CALCULATE: '/v1/fare/:calculate' },
  },
}));

vi.mock('@/infrastructure/config/env', () => ({
  env: { API_URL: 'http://localhost:3000', WS_URL: 'ws://localhost:3001' },
}));

vi.mock('@/infrastructure/stores/language-preference-store', () => ({
  useLanguagePreferenceStore: Object.assign(
    (selector: (s: Record<string, unknown>) => unknown) =>
      selector({ locale: 'en', syncFromServer: vi.fn(), setLocale: vi.fn() }),
    { getState: () => ({ locale: 'en', syncFromServer: vi.fn(), setLocale: vi.fn() }) },
  ),
}));

// Mock WebSocket for any remaining direct usage
class MockWebSocket {
  static OPEN = 1;
  readyState = 1;
  onopen: (() => void) | null = null;
  onmessage: ((e: { data: string }) => void) | null = null;
  onclose: ((e: { code: number }) => void) | null = null;
  onerror: (() => void) | null = null;
  send = vi.fn();
  close = vi.fn();
  constructor() {
    setTimeout(() => this.onopen?.(), 0);
  }
}

vi.stubGlobal('WebSocket', MockWebSocket);

// Mock navigator.geolocation for CommuterHeatmapPage
const mockGeolocation = {
  getCurrentPosition: vi.fn((success) =>
    success({ coords: { latitude: 14.5995, longitude: 120.9842 } }),
  ),
  watchPosition: vi.fn(),
  clearWatch: vi.fn(),
};
Object.defineProperty(navigator, 'geolocation', { value: mockGeolocation, writable: true });

// ─── Imports (after mocks) ───────────────────────────────────────────────────

import { LoginPage } from '@/features/auth/login/LoginPage';
import { CalculateFarePage } from '@/features/fare/calculate-fare/CalculateFarePage';
import { CommuterHeatmapPage } from '@/features/heatmap/commuter-heatmap/CommuterHeatmapPage';
import { DriverPaymentDashboardPage } from '@/features/payment/driver-dashboard/DriverPaymentDashboardPage';
import { CreateRoutePage } from '@/features/routes/create-route/CreateRoutePage';

// ─── Viewport Setup ──────────────────────────────────────────────────────────

/**
 * Set viewport width to 320px for mobile-first testing (Req 9.3).
 */
function setViewport320() {
  Object.defineProperty(window, 'innerWidth', { value: 320, writable: true });
  Object.defineProperty(window, 'innerHeight', { value: 568, writable: true });
  window.dispatchEvent(new Event('resize'));
}

// ─── Test Suites ─────────────────────────────────────────────────────────────

describe('Accessibility Audit — WCAG 2.1 AA (320px viewport)', () => {
  beforeEach(() => {
    setViewport320();
    vi.clearAllMocks();
  });

  describe('Login Page', () => {
    it('should have no WCAG 2.1 AA violations', async () => {
      const { container } = render(<LoginPage />);
      await expectNoA11yViolations(container);
    });

    it('should have accessible names on all interactive controls', () => {
      render(<LoginPage />);

      // Email input has associated label
      const emailInput = screen.getByLabelText('forms.email');
      expect(emailInput).toBeInTheDocument();
      expect(emailInput).toHaveAttribute('type', 'email');

      // Password input has associated label
      const passwordInput = screen.getByLabelText('forms.password');
      expect(passwordInput).toBeInTheDocument();
      expect(passwordInput).toHaveAttribute('type', 'password');

      // Submit button has accessible text
      const submitButton = screen.getByRole('button', { name: 'auth.login' });
      expect(submitButton).toBeInTheDocument();

      // Register link has accessible text
      const registerLink = screen.getByRole('link', { name: 'nav.register' });
      expect(registerLink).toBeInTheDocument();
    });

    it('should support keyboard-only form completion', async () => {
      const user = userEvent.setup();
      render(<LoginPage />);

      const emailInput = screen.getByLabelText('forms.email');
      const passwordInput = screen.getByLabelText('forms.password');
      const submitButton = screen.getByRole('button', { name: 'auth.login' });

      // Tab to email field
      await user.tab();
      expect(emailInput).toHaveFocus();

      // Tab to password field
      await user.tab();
      expect(passwordInput).toHaveFocus();

      // Tab to submit button
      await user.tab();
      expect(submitButton).toHaveFocus();
    });
  });

  describe('Fare Calculator Page', () => {
    it('should have no WCAG 2.1 AA violations', async () => {
      const { container } = render(<CalculateFarePage />);
      await expectNoA11yViolations(container);
    });

    it('should have accessible names on all interactive controls', () => {
      render(<CalculateFarePage />);

      // Origin latitude input
      expect(screen.getByLabelText('Latitude', { selector: '#origin-lat' })).toBeInTheDocument();

      // Origin longitude input
      expect(screen.getByLabelText('Longitude', { selector: '#origin-lng' })).toBeInTheDocument();

      // Destination latitude input
      expect(screen.getByLabelText('Latitude', { selector: '#dest-lat' })).toBeInTheDocument();

      // Destination longitude input
      expect(screen.getByLabelText('Longitude', { selector: '#dest-lng' })).toBeInTheDocument();

      // Vehicle type select
      expect(screen.getByLabelText('Vehicle Type')).toBeInTheDocument();

      // Discount category select
      expect(screen.getByLabelText('Discount Category')).toBeInTheDocument();

      // Submit button
      expect(screen.getByRole('button', { name: 'Calculate Fare' })).toBeInTheDocument();
    });

    it('should support keyboard-only fare calculation flow', async () => {
      const user = userEvent.setup();
      render(<CalculateFarePage />);

      // Tab through all form fields
      const originLat = screen.getByLabelText('Latitude', { selector: '#origin-lat' });
      const submitButton = screen.getByRole('button', { name: 'Calculate Fare' });

      // Focus should be reachable via keyboard
      await user.tab();
      expect(originLat).toHaveFocus();

      // Continue tabbing through all fields to reach submit
      await user.tab(); // origin lng
      await user.tab(); // dest lat
      await user.tab(); // dest lng
      await user.tab(); // vehicle type
      await user.tab(); // discount category
      await user.tab(); // submit button
      expect(submitButton).toHaveFocus();
    });
  });

  describe('Commuter Heatmap Page (Waiting for Ride)', () => {
    it('should have no WCAG 2.1 AA violations', async () => {
      const { container } = render(<CommuterHeatmapPage />);
      await vi.waitFor(() => {
        expect(screen.queryByText('Getting your location...')).not.toBeInTheDocument();
      });
      await expectNoA11yViolations(container);
    });

    it('should have accessible names on all actionable controls including map markers', async () => {
      render(<CommuterHeatmapPage />);
      await vi.waitFor(() => {
        expect(screen.queryByText('Getting your location...')).not.toBeInTheDocument();
      });

      // Vehicle type select has accessible label
      expect(screen.getByLabelText('Select vehicle type')).toBeInTheDocument();

      // Demand ping button has accessible label (Req 9.5 — map markers for Demand_Pings)
      expect(
        screen.getByRole('button', { name: 'Signal that you are waiting for a ride here' }),
      ).toBeInTheDocument();

      // Map marker has accessible name
      expect(screen.getByLabelText('Your location')).toBeInTheDocument();

      // Map container has accessible role and label
      expect(screen.getByRole('application', { name: 'Commuter location map' })).toBeInTheDocument();
    });

    it('should support keyboard-only waiting-for-ride flow', async () => {
      const user = userEvent.setup();
      render(<CommuterHeatmapPage />);
      await vi.waitFor(() => {
        expect(screen.queryByText('Getting your location...')).not.toBeInTheDocument();
      });

      // Tab to vehicle type selector
      const vehicleSelect = screen.getByLabelText('Select vehicle type');
      const pingButton = screen.getByRole('button', {
        name: 'Signal that you are waiting for a ride here',
      });

      // Focus should reach the vehicle select and ping button via keyboard
      vehicleSelect.focus();
      expect(vehicleSelect).toHaveFocus();

      await user.tab();
      expect(pingButton).toHaveFocus();
    });
  });

  describe('Driver Payment Dashboard', () => {
    it('should have no WCAG 2.1 AA violations', async () => {
      const { container } = render(<DriverPaymentDashboardPage />);
      await expectNoA11yViolations(container);
    });

    it('should have accessible names on dashboard elements', () => {
      render(<DriverPaymentDashboardPage />);

      // Page heading
      expect(screen.getByRole('heading', { name: 'Payment Dashboard' })).toBeInTheDocument();

      // Payment notifications area has accessible label
      expect(screen.getByRole('main', { name: 'Payment notifications' })).toBeInTheDocument();
    });
  });

  describe('Create Route Page', () => {
    it('should have no WCAG 2.1 AA violations', async () => {
      const { container } = render(<CreateRoutePage />);
      await expectNoA11yViolations(container);
    });

    it('should have accessible heading and descriptive text', () => {
      render(<CreateRoutePage />);

      expect(screen.getByRole('heading', { name: 'Create New Route' })).toBeInTheDocument();
      expect(
        screen.getByText(/Plot at least 2 waypoints/),
      ).toBeInTheDocument();
    });
  });

  describe('Focus Indicators (Req 9.4)', () => {
    it('login form inputs should have visible focus styles via CSS classes', () => {
      render(<LoginPage />);

      const emailInput = screen.getByLabelText('forms.email');
      const passwordInput = screen.getByLabelText('forms.password');
      const submitButton = screen.getByRole('button', { name: 'auth.login' });

      // Verify focus-related CSS classes are present
      expect(emailInput.className).toContain('focus:');
      expect(passwordInput.className).toContain('focus:');
      expect(submitButton.className).toContain('focus:');
    });

    it('fare calculator controls should have visible focus styles', () => {
      render(<CalculateFarePage />);

      const originLat = screen.getByLabelText('Latitude', { selector: '#origin-lat' });
      const vehicleType = screen.getByLabelText('Vehicle Type');
      const submitButton = screen.getByRole('button', { name: 'Calculate Fare' });

      expect(originLat.className).toContain('focus:');
      expect(vehicleType.className).toContain('focus:');
      expect(submitButton.className).toContain('focus:');
    });
  });

  describe('Hit Target Size (Req 9.1)', () => {
    it('fare calculator buttons and inputs should have min-h-[44px] class', () => {
      render(<CalculateFarePage />);

      const submitButton = screen.getByRole('button', { name: 'Calculate Fare' });
      expect(submitButton.className).toContain('min-h-[44px]');

      const originLat = screen.getByLabelText('Latitude', { selector: '#origin-lat' });
      expect(originLat.className).toContain('min-h-[44px]');
    });

    it('commuter heatmap buttons should have min-h-[44px] class', async () => {
      render(<CommuterHeatmapPage />);
      await vi.waitFor(() => {
        expect(screen.queryByText('Getting your location...')).not.toBeInTheDocument();
      });

      const pingButton = screen.getByRole('button', {
        name: 'Signal that you are waiting for a ride here',
      });
      expect(pingButton.className).toContain('min-h-[44px]');

      const vehicleSelect = screen.getByLabelText('Select vehicle type');
      expect(vehicleSelect.className).toContain('min-h-[44px]');
    });
  });
});
