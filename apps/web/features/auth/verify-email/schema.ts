/**
 * Zod validation schema for email verification.
 *
 * The token is read from the URL query parameter, so validation
 * ensures it's a non-empty string.
 */

import { z } from 'zod';

export const verifyEmailSchema = z.object({
  token: z.string().min(1, 'forms.required'),
});

export type VerifyEmailFormData = z.infer<typeof verifyEmailSchema>;
