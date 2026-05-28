/**
 * Promote user feature types — mirrors POST /v1/admin/users/{id}/:promote.
 *
 * Feature-scoped; evolves independently from other admin features.
 * Requirements: 5.9, 5.11
 */

export type UserRole = 'commuter' | 'driver' | 'moderator' | 'super_admin';

export interface PromoteUserRequest {
  /** The acting Super Admin's password for 2FA confirmation */
  password: string;
  /** The target role to assign */
  newRole: UserRole;
}

export interface PromoteUserResponse {
  userId: string;
  newRole: string;
  message: string;
}
