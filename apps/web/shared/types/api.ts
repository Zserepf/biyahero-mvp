/**
 * Shared API envelope types matching the backend response shape.
 *
 * Every non-2xx response from the backend returns:
 * { error: { code, message, details } }
 */

export interface ApiErrorEnvelope {
  error: {
    /** Machine-readable error code (e.g. "auth.unauthenticated", "input.validation_failed") */
    code: string;
    /** Human-readable error message */
    message: string;
    /** Optional structured details (e.g. field-level validation errors) */
    details?: Record<string, unknown>;
  };
}

/**
 * Normalized error thrown by the Axios error interceptor.
 * Features never parse raw Axios errors — they receive this shape.
 */
export class ApiError extends Error {
  /** Machine-readable error code from the backend */
  readonly code: string;
  /** HTTP status code */
  readonly status: number;
  /** Optional structured details */
  readonly details?: Record<string, unknown>;

  constructor(
    status: number,
    code: string,
    message: string,
    details?: Record<string, unknown>,
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.details = details;
  }
}
