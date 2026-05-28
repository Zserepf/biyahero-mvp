/**
 * Refresh feature types — mirrors POST /v1/auth/sessions/:refresh.
 *
 * Feature-scoped; evolves independently from other auth features.
 */

export interface RefreshRequest {
  refreshToken: string;
}

export interface RefreshResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}
