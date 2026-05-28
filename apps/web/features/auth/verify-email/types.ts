/**
 * Verify-email feature types — mirrors POST /v1/auth/email-verifications/:verify.
 *
 * Feature-scoped; evolves independently from other auth features.
 */

export interface VerifyEmailRequest {
  token: string;
}

export interface VerifyEmailResponse {
  message: string;
}
