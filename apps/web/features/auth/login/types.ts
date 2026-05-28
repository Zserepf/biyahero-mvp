/**
 * Login feature types — mirrors POST /v1/auth/sessions.
 *
 * Feature-scoped; evolves independently from other auth features.
 */

export type UserRole = 'commuter' | 'driver' | 'moderator' | 'super_admin';

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
