/**
 * Component tests for the Driver Payment Dashboard feature slice.
 *
 * Happy path: payment confirmed event displays payer name, amount, and timestamp.
 * Error path: disconnected state shown when WebSocket is not connected.
 *
 * Requirements: 3.3 (payment confirmed display)
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PaymentNotification } from '@/features/payment/driver-dashboard/PaymentNotification';
import { DriverPaymentDashboardPage } from '@/features/payment/driver-dashboard/DriverPaymentDashboardPage';
import type { PaymentNotificationItem } from '@/features/payment/driver-dashboard/types';

// ─── Mock the hooks ──────────────────────────────────────────────────────────

const mockPayments: { eventId: string; payerName: string; amountCentavos: number; occurredAt: string; driverId: string; payerId: string; routeId: string; currency: string }[] = [];
let mockIsConnected = true;
let mockError: string | null = null;

vi.mock('@/features/payment/driver-dashboard/usePaymentListener', () => ({
  usePaymentListener: () => ({
    payments: mockPayments,
    isConnected: mockIsConnected,
    error: mockError,
  }),
}));

vi.mock('@/features/payment/driver-dashboard/useAudioConfirmation', () => ({
  useAudioConfirmation: () => ({
    playConfirmation: vi.fn().mockResolvedValue(true),
  }),
}));

describe('PaymentNotification', () => {
  it('displays payer name, amount, and timestamp for a confirmed payment', () => {
    const notification: PaymentNotificationItem = {
      eventId: 'evt-1',
      payerName: 'Juan Dela Cruz',
      formattedAmount: '₱25.00',
      amountCentavos: 2500,
      formattedTime: '10:30:45 AM',
      occurredAt: '2024-01-15T10:30:45.000Z',
      audioPlayed: true,
    };

    render(<PaymentNotification notification={notification} />);

    expect(screen.getByText('Juan Dela Cruz')).toBeInTheDocument();
    expect(screen.getByText('₱25.00')).toBeInTheDocument();
    expect(screen.getByText('10:30:45 AM')).toBeInTheDocument();
  });

  it('shows "No audio" indicator when audio was not played', () => {
    const notification: PaymentNotificationItem = {
      eventId: 'evt-2',
      payerName: 'Maria Santos',
      formattedAmount: '₱50.00',
      amountCentavos: 5000,
      formattedTime: '11:00:00 AM',
      occurredAt: '2024-01-15T11:00:00.000Z',
      audioPlayed: false,
    };

    render(<PaymentNotification notification={notification} />);

    expect(
      screen.getByLabelText('Audio confirmation was not played'),
    ).toBeInTheDocument();
  });
});

describe('DriverPaymentDashboardPage', () => {
  beforeEach(() => {
    mockPayments.length = 0;
    mockIsConnected = true;
    mockError = null;
  });

  it('happy path: shows "Payment Dashboard" header and connected status', () => {
    render(<DriverPaymentDashboardPage />);

    expect(screen.getByText('Payment Dashboard')).toBeInTheDocument();
    expect(screen.getByText('Connected')).toBeInTheDocument();
  });

  it('shows empty state when no payments received', () => {
    render(<DriverPaymentDashboardPage />);

    expect(
      screen.getByText(/No payments received yet/),
    ).toBeInTheDocument();
  });

  it('error path: shows disconnected status when WebSocket is not connected', () => {
    mockIsConnected = false;

    render(<DriverPaymentDashboardPage />);

    expect(screen.getByText('Disconnected')).toBeInTheDocument();
  });

  it('error path: displays error message when connection error occurs', () => {
    mockError = 'WebSocket connection failed';

    render(<DriverPaymentDashboardPage />);

    expect(screen.getByRole('alert')).toHaveTextContent(
      'WebSocket connection failed',
    );
  });
});
