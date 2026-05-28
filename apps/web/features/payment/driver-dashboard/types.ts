/**
 * Driver payment dashboard types — defines the WebSocket payment.confirmed
 * event shape and audio failure reporting.
 *
 * Requirements: 3.3, 3.8
 */

// ─── WebSocket Event ─────────────────────────────────────────────────────────

/**
 * The `payment.confirmed` envelope pushed by the WebSocket_Service
 * when a passenger's digital-wallet payment completes.
 */
export interface PaymentConfirmedEvent {
  /** Unique payment event ID (idempotence key) */
  eventId: string;
  /** Driver receiving the payment */
  driverId: string;
  /** Payer (commuter) ID */
  payerId: string;
  /** Display name of the payer shown on the dashboard */
  payerName: string;
  /** Route associated with this payment */
  routeId: string;
  /** Amount in centavos (integer) — divide by 100 for PHP display */
  amountCentavos: number;
  /** Currency code (always "PHP" for MVP) */
  currency: string;
  /** ISO 8601 timestamp when the payment occurred */
  occurredAt: string;
}

// ─── Audio Failure Report ────────────────────────────────────────────────────

/**
 * Payload POSTed to `/v1/payments/audio-failures` when the driver's device
 * cannot play the TTS Audio_Confirmation (muted, autoplay blocked, etc.).
 *
 * Requirement: 3.8
 */
export interface AudioFailureReport {
  /** The payment event that failed to produce audio */
  eventId: string;
  /** Driver who should have heard the confirmation */
  driverId: string;
  /** Reason the audio could not play */
  reason: 'muted' | 'autoplay_blocked' | 'no_voice_available' | 'unknown';
  /** ISO 8601 timestamp of the failure */
  occurredAt: string;
}

// ─── Notification Display ────────────────────────────────────────────────────

/**
 * Processed payment notification ready for UI rendering.
 */
export interface PaymentNotificationItem {
  eventId: string;
  payerName: string;
  /** Formatted amount string (e.g. "₱25.00") */
  formattedAmount: string;
  /** Raw amount in centavos for sorting/calculations */
  amountCentavos: number;
  /** Formatted timestamp for display */
  formattedTime: string;
  /** Raw ISO timestamp */
  occurredAt: string;
  /** Whether audio played successfully for this notification */
  audioPlayed: boolean;
}
