/**
 * Component tests for the Route-Plot (Create Route) feature slice.
 *
 * Happy path: route submission with valid waypoints.
 * Error path: validation error when fewer than 2 waypoints.
 *
 * Requirements: 1.1 (route submission)
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CreateRouteForm } from '@/features/routes/create-route/CreateRouteForm';

// Mock the RouteMap component since it depends on Leaflet
vi.mock('@/features/routes/components/RouteMap', () => ({
  RouteMap: ({
    onWaypointAdd,
  }: {
    onWaypointAdd?: (lat: number, lng: number) => void;
    waypoints?: unknown[];
    editable?: boolean;
    height?: string;
  }) => (
    <div data-testid="route-map">
      <button
        data-testid="add-waypoint-btn"
        onClick={() => onWaypointAdd?.(14.5995, 120.9842)}
      >
        Add Waypoint 1
      </button>
      <button
        data-testid="add-waypoint-btn-2"
        onClick={() => onWaypointAdd?.(14.5547, 121.0244)}
      >
        Add Waypoint 2
      </button>
    </div>
  ),
}));

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>,
  );
}

describe('CreateRouteForm', () => {
  beforeEach(() => {
    localStorage.setItem('biyahero_access_token', 'mock-token');
  });

  it('renders the form with name, vehicle type, base fare, and map', () => {
    renderWithProviders(<CreateRouteForm />);

    expect(screen.getByLabelText('Route Name')).toBeInTheDocument();
    expect(screen.getByLabelText('Vehicle Type')).toBeInTheDocument();
    expect(screen.getByLabelText('Base Fare (PHP)')).toBeInTheDocument();
    expect(screen.getByTestId('route-map')).toBeInTheDocument();
  });

  it('submit button is disabled when fewer than 2 waypoints', () => {
    renderWithProviders(<CreateRouteForm />);

    const submitBtn = screen.getByRole('button', { name: 'Submit route' });
    expect(submitBtn).toBeDisabled();
  });

  it('happy path: submits route successfully with ≥2 waypoints', async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();

    renderWithProviders(<CreateRouteForm onSuccess={onSuccess} />);

    // Fill in route name
    await user.type(screen.getByLabelText('Route Name'), 'Cubao to Antipolo');

    // Add two waypoints via the mocked map
    await user.click(screen.getByTestId('add-waypoint-btn'));
    await user.click(screen.getByTestId('add-waypoint-btn-2'));

    // Submit button should now be enabled
    const submitBtn = screen.getByRole('button', { name: 'Submit route' });
    expect(submitBtn).not.toBeDisabled();

    await user.click(submitBtn);

    await waitFor(() => {
      expect(onSuccess).toHaveBeenCalled();
    });
  });

  it('error path: shows validation error when name is empty', async () => {
    const user = userEvent.setup();

    renderWithProviders(<CreateRouteForm />);

    // Add two waypoints but leave name empty
    await user.click(screen.getByTestId('add-waypoint-btn'));
    await user.click(screen.getByTestId('add-waypoint-btn-2'));

    // Clear the default name (it starts empty, so just submit)
    const submitBtn = screen.getByRole('button', { name: 'Submit route' });
    await user.click(submitBtn);

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });
});
