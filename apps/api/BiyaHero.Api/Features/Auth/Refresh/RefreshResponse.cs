namespace BiyaHero.Api.Features.Auth.Refresh;

/// <summary>
/// Response body for a successful token refresh.
/// Contains new JWT access and refresh tokens (token rotation).
/// </summary>
public sealed record RefreshResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);
