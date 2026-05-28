namespace BiyaHero.Api.Features.Auth.VerifyEmail;

/// <summary>
/// Abstraction for storing and validating email verification tokens.
/// MVP uses an in-memory implementation; can be replaced with Redis/DynamoDB later.
/// </summary>
public interface IVerificationTokenStore
{
    /// <summary>
    /// Stores a verification token for the given user with an expiry duration.
    /// </summary>
    /// <param name="userId">The user ID the token belongs to.</param>
    /// <param name="token">The verification token string.</param>
    /// <param name="expiry">How long the token remains valid.</param>
    Task StoreTokenAsync(Guid userId, string token, TimeSpan expiry);

    /// <summary>
    /// Validates a token and consumes it (single-use).
    /// Returns the associated user ID if the token is valid and not expired.
    /// Returns null if the token is invalid, expired, or already consumed.
    /// </summary>
    /// <param name="token">The verification token to validate.</param>
    /// <returns>The user ID if valid; null otherwise.</returns>
    Task<Guid?> ValidateAndConsumeTokenAsync(string token);
}
