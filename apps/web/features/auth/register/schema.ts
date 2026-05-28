/**
 * Zod validation schema for the registration form.
 *
 * Client-side validation for instant UX feedback:
 * - Email format
 * - Password min 8 chars (Req 5.1)
 * - Required fields (displayName, role)
 * - Password confirmation match
 *
 * The backend remains the source of truth for business rules (email uniqueness).
 */

import { z } from 'zod';

export const registerSchema = z
  .object({
    email: z.string().min(1, 'forms.required').email('forms.invalidEmail'),
    password: z.string().min(8, 'forms.passwordTooShort'),
    confirmPassword: z.string().min(1, 'forms.required'),
    displayName: z.string().min(1, 'forms.required').max(50, 'forms.displayNameTooLong'),
    role: z.enum(['commuter', 'driver'], {
      message: 'forms.required',
    }),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: 'forms.passwordMismatch',
    path: ['confirmPassword'],
  });

export type RegisterFormData = z.infer<typeof registerSchema>;
