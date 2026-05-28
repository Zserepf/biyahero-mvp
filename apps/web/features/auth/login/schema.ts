/**
 * Zod validation schema for the login form.
 *
 * Client-side validation for instant UX feedback:
 * - Email format
 * - Required fields
 *
 * The backend remains the source of truth for credential verification.
 */

import { z } from 'zod';

export const loginSchema = z.object({
  email: z.string().min(1, 'forms.required').email('forms.invalidEmail'),
  password: z.string().min(1, 'forms.required'),
});

export type LoginFormData = z.infer<typeof loginSchema>;
