namespace BiyaHero.Api.Features.Auth.Me;

/// <summary>
/// Request body for PATCH /v1/auth/me/language-preference.
/// </summary>
public sealed record UpdateLanguagePreferenceRequest(string LanguagePreference);
