/**
 * Promote user Zod validation schema.
 *
 * Validates the password re-entry and role selection before submission.
 * Requirements: 5.11
 */

import { z } from 'zod';

export const promoteUserSchema = z.object({
  password: z
    .string()
    .min(1, 'validation.passwordRequired'),
  newRole: z.enum(['commuter', 'driver', 'moderator', 'super_admin'], {
    message: 'validation.roleRequired',
  }),
});

export type PromoteUserFormData = z.infer<typeof promoteUserSchema>;
