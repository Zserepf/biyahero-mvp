namespace BiyaHero.Api.Features.Auth.Me;

/// <summary>
/// Response body for PATCH /v1/auth/me/language-preference.
/// Returns the updated language preference.
/// </summary>
public sealed record UpdateLanguagePreferenceResponse(string LanguagePreference);
