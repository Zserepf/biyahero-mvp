namespace BiyaHero.Api.Features.Auth.Login;

/// <summary>
/// Response body for a successful login.
/// Contains JWT tokens and basic user info.
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    LoginUserDto User);

/// <summary>
/// Minimal user info returned alongside login tokens.
/// </summary>
public sealed record LoginUserDto(
    Guid Id,
    string Email,
    string Role,
    string DisplayName,
    string LanguagePreference);
