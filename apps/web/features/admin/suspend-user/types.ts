/**
 * Suspend user feature types — mirrors POST /v1/admin/users/{id}/:suspend response.
 *
 * Feature-scoped; evolves independently from other admin features.
 * Requirements: 5.8
 */

export interface SuspendUserResponse {
  userId: string;
  status: string;
  message: string;
}
