using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Auth.Logout;

/// <summary>
/// Business logic for user logout (DELETE /v1/auth/sessions/{id}).
/// For MVP, logout is client-side (client discards tokens).
/// This endpoint validates the access token and returns 204.
/// In the future, this could add the token to a blocklist.
/// Requirements: 5.3
/// </summary>
public sealed class LogoutHandler
{
    private readonly IJwtService _jwtService;

    public LogoutHandler(IJwtService jwtService)
    {
        _jwtService = jwtService;
    }

    /// <summary>
    /// Validates the access token from the Authorization header.
    /// Throws UnauthenticatedException if the token is invalid.
    /// For MVP, no server-side token revocation is performed.
    /// </summary>
    public async Task HandleAsync(string? authorizationHeader, CancellationToken cancellationToken = default)
    {
        var token = ExtractBearerToken(authorizationHeader);

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new UnauthenticatedException("Authentication required.");
        }

        var result = await _jwtService.ValidateTokenDetailedAsync(token, cancellationToken);

        if (!result.IsValid)
        {
            throw new UnauthenticatedException("Invalid or expired token.");
        }

        // MVP: No server-side revocation. Client discards tokens.
        // Future: Add token to a blocklist here.
    }

    private static string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return null;

        if (authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authorizationHeader["Bearer ".Length..].Trim();

        return null;
    }
}
