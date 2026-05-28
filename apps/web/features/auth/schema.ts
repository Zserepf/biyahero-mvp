/**
 * Zod validation schemas for auth forms.
 *
 * Client-side validation for instant UX feedback.
 * The backend remains the source of truth for business rules.
 */

import { z } from 'zod';

// ─── Login Schema ────────────────────────────────────────────────────────────

export const loginSchema = z.object({
  email: z
    .string()
    .min(1, 'forms.required')
    .email('forms.invalidEmail'),
  password: z
    .string()
    .min(1, 'forms.required'),
});

export type LoginFormData = z.infer<typeof loginSchema>;

// ─── Registration Schema ─────────────────────────────────────────────────────

export const registerSchema = z.object({
  email: z
    .string()
    .min(1, 'forms.required')
    .email('forms.invalidEmail'),
  password: z
    .string()
    .min(8, 'forms.passwordTooShort'),
  confirmPassword: z
    .string()
    .min(1, 'forms.required'),
  displayName: z
    .string()
    .min(1, 'forms.required')
    .max(50, 'forms.required'),
  role: z.enum(['commuter', 'driver'], {
    message: 'forms.required',
  }),
}).refine((data) => data.password === data.confirmPassword, {
  message: 'forms.passwordMismatch',
  path: ['confirmPassword'],
});

export type RegisterFormData = z.infer<typeof registerSchema>;
