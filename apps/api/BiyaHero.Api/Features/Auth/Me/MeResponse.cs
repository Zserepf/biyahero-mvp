namespace BiyaHero.Api.Features.Auth.Me;

/// <summary>
/// Response body for GET /v1/auth/me.
/// Returns the authenticated user's profile information.
/// </summary>
public sealed record MeResponse(
    Guid Id,
    string Email,
    string Role,
    string DisplayName,
    string LanguagePreference);
