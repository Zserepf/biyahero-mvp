'use client';

/**
 * TanStack Query provider — wraps the app in QueryClientProvider.
 *
 * Creates a stable QueryClient instance per React tree.
 */

import { useState } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from './query-client';

export function QueryProvider({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(() => createQueryClient());

  return (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}
