/**
 * Register feature types — mirrors the backend POST /v1/auth/registrations.
 *
 * Feature-scoped; evolves independently from other auth features.
 */

export type RegisterRole = 'commuter' | 'driver';

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
  role: RegisterRole;
}

export interface RegisterResponse {
  userId: string;
  email: string;
  message: string;
}
