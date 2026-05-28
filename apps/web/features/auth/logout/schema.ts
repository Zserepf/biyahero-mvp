/**
 * Zod validation schema for logout.
 *
 * Logout has no user input — this schema exists for structural consistency
 * with the feature-sliced pattern.
 */

import { z } from 'zod';

export const logoutSchema = z.object({});

export type LogoutFormData = z.infer<typeof logoutSchema>;
