/**
 * Auth feature types — mirrors the backend Auth request/response shapes.
 *
 * These types are feature-scoped and evolve independently from other features.
 */

// ─── Enums ───────────────────────────────────────────────────────────────────

export type UserRole = 'commuter' | 'driver' | 'moderator' | 'super_admin';

export type UserStatus = 'pending_verification' | 'active' | 'suspended';

// ─── Login ───────────────────────────────────────────────────────────────────

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginUserDto {
  id: string;
  email: string;
  role: UserRole;
  displayName: string;
  languagePreference: 'en' | 'fil';
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: LoginUserDto;
}

// ─── Registration ────────────────────────────────────────────────────────────

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
  role: 'commuter' | 'driver';
}

export interface RegisterResponse {
  userId: string;
  email: string;
  message: string;
}

// ─── Email Verification ──────────────────────────────────────────────────────

export interface VerifyEmailRequest {
  token: string;
}

export interface VerifyEmailResponse {
  message: string;
}

// ─── Me (Current User Profile) ───────────────────────────────────────────────

export interface MeResponse {
  id: string;
  email: string;
  role: UserRole;
  displayName: string;
  languagePreference: 'en' | 'fil';
}

// ─── Token Refresh ───────────────────────────────────────────────────────────

export interface RefreshRequest {
  refreshToken: string;
}

export interface RefreshResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}
