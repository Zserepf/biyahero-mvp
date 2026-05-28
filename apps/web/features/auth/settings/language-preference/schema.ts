/**
 * Zod validation schema for language preference update.
 *
 * Validates that the selected language is one of the supported locales.
 */

import { z } from 'zod';

export const languagePreferenceSchema = z.object({
  languagePreference: z.enum(['en', 'fil'], {
    message: 'forms.required',
  }),
});

export type LanguagePreferenceFormData = z.infer<typeof languagePreferenceSchema>;
