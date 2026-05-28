namespace BiyaHero.Api.Features.Payment;

/// <summary>
/// Verifies HMAC-SHA256 webhook signatures from the wallet provider.
/// </summary>
public interface IWebhookSignatureVerifier
{
    /// <summary>
    /// Verifies the webhook signature and timestamp headers against the raw request body.
    /// </summary>
    /// <param name="rawBody">The raw request body bytes.</param>
    /// <param name="signature">The value of the X-Wallet-Signature header.</param>
    /// <param name="timestamp">The value of the X-Wallet-Timestamp header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating whether the signature is valid or invalid with a reason.</returns>
    Task<WebhookSignatureResult> VerifyAsync(
        byte[] rawBody,
        string? signature,
        string? timestamp,
        CancellationToken cancellationToken = default);
}
