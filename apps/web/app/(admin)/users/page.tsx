import { redirect } from 'next/navigation';

export default function AdminUsersLegacyRedirect() {
  redirect('/admin/users');
}
