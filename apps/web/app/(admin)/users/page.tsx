/**
 * Admin user management page route.
 *
 * Protected by AuthGuard — only Super Admin users can access.
 * Requirements: 5.8, 5.9, 5.11
 */

import { UserListPage } from '@/features/admin/user-list/UserListPage';

export default function AdminUsersPage() {
  return <UserListPage />;
}
