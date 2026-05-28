/**
 * Zod validation schema for token refresh.
 *
 * Validates that a refresh token string is present before sending to the API.
 */

import { z } from 'zod';

export const refreshSchema = z.object({
  refreshToken: z.string().min(1, 'forms.required'),
});

export type RefreshFormData = z.infer<typeof refreshSchema>;
