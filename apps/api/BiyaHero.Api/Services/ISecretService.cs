namespace BiyaHero.Api.Services;

/// <summary>
/// Provides access to application secrets (JWT signing key, webhook signing secret).
/// Implementations must be thread-safe for concurrent Lambda invocations.
/// </summary>
public interface ISecretService
{
    /// <summary>
    /// Returns the HS256 JWT signing key bytes used for token issuance and verification.
    /// </summary>
    Task<byte[]> GetJwtSigningKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the HMAC-SHA256 webhook signing secret bytes used for payment webhook verification.
    /// </summary>
    Task<byte[]> GetWebhookSigningSecretAsync(CancellationToken cancellationToken = default);
}
