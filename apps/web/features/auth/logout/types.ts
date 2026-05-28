/**
 * Logout feature types — mirrors DELETE /v1/auth/sessions/{id}.
 *
 * Feature-scoped; evolves independently from other auth features.
 */

/** No request body needed — session ID is in the URL path */
export interface LogoutResult {
  success: boolean;
}
