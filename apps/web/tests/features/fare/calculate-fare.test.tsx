/**
 * Component tests for the Fare Calculator feature slice.
 *
 * Happy path: fare calculation with valid inputs returns result.
 * Error path: invalid input (missing vehicle type) shows validation error.
 *
 * Requirements: 2.1 (fare calculation)
 */

import { describe, it, expect } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CalculateFareForm } from '@/features/fare/calculate-fare/CalculateFareForm';

// Mock the FareResult component to simplify assertions
vi.mock('@/features/fare/calculate-fare/FareResult', () => ({
  FareResult: ({ result }: { result: { amountPhp: number; distanceKm: number; matrixVersion: string } }) => (
    <div data-testid="fare-result">
      <span data-testid="fare-amount">₱{result.amountPhp.toFixed(2)}</span>
      <span data-testid="fare-distance">{result.distanceKm} km</span>
      <span data-testid="fare-version">{result.matrixVersion}</span>
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

describe('CalculateFareForm', () => {
  it('renders origin, destination, vehicle type, and discount fields', () => {
    renderWithProviders(<CalculateFareForm />);

    expect(screen.getByLabelText('Latitude', { selector: '#origin-lat' })).toBeInTheDocument();
    expect(screen.getByLabelText('Longitude', { selector: '#origin-lng' })).toBeInTheDocument();
    expect(screen.getByLabelText('Latitude', { selector: '#dest-lat' })).toBeInTheDocument();
    expect(screen.getByLabelText('Longitude', { selector: '#dest-lng' })).toBeInTheDocument();
    expect(screen.getByLabelText('Vehicle Type')).toBeInTheDocument();
    expect(screen.getByLabelText('Discount Category')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Calculate Fare' })).toBeInTheDocument();
  });

  it('happy path: calculates fare with valid inputs and displays result', async () => {
    const user = userEvent.setup();

    renderWithProviders(<CalculateFareForm />);

    // Fill in valid coordinates within Philippines bbox
    await user.type(screen.getByLabelText('Latitude', { selector: '#origin-lat' }), '14.5995');
    await user.type(screen.getByLabelText('Longitude', { selector: '#origin-lng' }), '120.9842');
    await user.type(screen.getByLabelText('Latitude', { selector: '#dest-lat' }), '14.5547');
    await user.type(screen.getByLabelText('Longitude', { selector: '#dest-lng' }), '121.0244');

    // Select vehicle type
    await user.selectOptions(screen.getByLabelText('Vehicle Type'), 'Jeepney');

    // Submit
    await user.click(screen.getByRole('button', { name: 'Calculate Fare' }));

    // Wait for result to appear
    await waitFor(() => {
      expect(screen.getByTestId('fare-result')).toBeInTheDocument();
    });

    expect(screen.getByTestId('fare-amount')).toHaveTextContent('₱13.00');
    expect(screen.getByTestId('fare-distance')).toHaveTextContent('2.5 km');
    expect(screen.getByTestId('fare-version')).toHaveTextContent('v1');
  });

  it('error path: shows validation error when vehicle type is not selected', async () => {
    const user = userEvent.setup();

    renderWithProviders(<CalculateFareForm />);

    // Fill in valid coordinates but leave vehicle type empty
    await user.type(screen.getByLabelText('Latitude', { selector: '#origin-lat' }), '14.5995');
    await user.type(screen.getByLabelText('Longitude', { selector: '#origin-lng' }), '120.9842');
    await user.type(screen.getByLabelText('Latitude', { selector: '#dest-lat' }), '14.5547');
    await user.type(screen.getByLabelText('Longitude', { selector: '#dest-lng' }), '121.0244');

    // Submit without selecting vehicle type
    await user.click(screen.getByRole('button', { name: 'Calculate Fare' }));

    // Should show validation error
    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    // Result should NOT be displayed
    expect(screen.queryByTestId('fare-result')).not.toBeInTheDocument();
  });
});
