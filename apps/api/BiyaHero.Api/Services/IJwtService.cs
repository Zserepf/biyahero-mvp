using System.Security.Claims;
using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Services;

/// <summary>
/// Issues and validates JWT tokens using HS256 with a KMS-backed signing key.
/// Access tokens expire in 24 hours; refresh tokens expire in 30 days.
/// Requirements: 5.3, 5.4, 5.7
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generates a 24-hour access token containing userId, email, and role claims.
    /// </summary>
    Task<string> GenerateAccessTokenAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a 30-day refresh token containing userId and token type claim.
    /// </summary>
    Task<string> GenerateRefreshTokenAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a JWT token's signature, expiration, and required claims.
    /// Returns a ClaimsPrincipal if valid, or null if the token is invalid or expired.
    /// </summary>
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the user ID (sub claim) from a token without full validation.
    /// Returns null if the token is invalid, expired, or missing the sub claim.
    /// </summary>
    Task<Guid?> GetUserIdFromTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a JWT token and returns detailed result information.
    /// Use this when you need error details beyond pass/fail.
    /// </summary>
    Task<JwtValidationResult> ValidateTokenDetailedAsync(string token, CancellationToken cancellationToken = default);
}
