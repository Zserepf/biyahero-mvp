/**
 * Admin user list feature types — mirrors GET /v1/admin/users response.
 *
 * Feature-scoped; evolves independently from other admin features.
 * Requirements: 5.8, 5.9
 */

export type UserRole = 'commuter' | 'driver' | 'moderator' | 'super_admin';

export type UserStatus = 'pending_verification' | 'active' | 'suspended';

export interface AdminUserItem {
  id: string;
  email: string;
  role: UserRole;
  status: UserStatus;
  displayName: string;
  languagePreference: 'en' | 'fil';
  createdAt: string;
  updatedAt: string;
}

export type AdminUsersResponse = AdminUserItem[];
