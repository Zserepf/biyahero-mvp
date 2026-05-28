/**
 * Language preference feature types — mirrors PATCH /v1/auth/me/language-preference.
 *
 * Feature-scoped; evolves independently from other auth features.
 */

export type LanguagePreference = 'en' | 'fil';

export interface UpdateLanguagePreferenceRequest {
  languagePreference: LanguagePreference;
}

export interface UpdateLanguagePreferenceResponse {
  languagePreference: LanguagePreference;
}
