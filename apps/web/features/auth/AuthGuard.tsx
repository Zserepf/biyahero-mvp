'use client';

/**
 * AuthGuard — protects routes that require authentication.
 *
 * Redirects unauthenticated users to the login page.
 * Shows a loading spinner while checking auth status.
 *
 * Usage:
 *   <AuthGuard>
 *     <ProtectedContent />
 *   </AuthGuard>
 */

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useMe } from './useMe';

interface AuthGuardProps {
  children: React.ReactNode;
  /** Optional: required role(s) for access */
  requiredRoles?: string[];
}

export function AuthGuard({ children, requiredRoles }: AuthGuardProps) {
  const { user, isLoading } = useMe();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && !user) {
      router.replace('/login');
    }
  }, [isLoading, user, router]);

  useEffect(() => {
    if (!isLoading && user && requiredRoles && requiredRoles.length > 0) {
      if (!requiredRoles.includes(user.role)) {
        router.replace('/');
      }
    }
  }, [isLoading, user, requiredRoles, router]);

  if (isLoading) {
    return (
      <div
        className="flex min-h-screen items-center justify-center"
        role="status"
        aria-label="Loading"
      >
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-blue-600 border-t-transparent" />
      </div>
    );
  }

  if (!user) {
    return null;
  }

  if (requiredRoles && requiredRoles.length > 0 && !requiredRoles.includes(user.role)) {
    return null;
  }

  return <>{children}</>;
}
