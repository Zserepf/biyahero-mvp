/**
 * Driver payment dashboard feature slice — public API.
 *
 * Exports the page component and hooks for use in the app router.
 */

export { DriverPaymentDashboardPage } from './DriverPaymentDashboardPage';
export { usePaymentListener } from './usePaymentListener';
export { useAudioConfirmation } from './useAudioConfirmation';
export { PaymentNotification } from './PaymentNotification';
export { AudioFallbackBanner } from './AudioFallbackBanner';
export type {
  PaymentConfirmedEvent,
  AudioFailureReport,
  PaymentNotificationItem,
} from './types';
