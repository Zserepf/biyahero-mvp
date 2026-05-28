using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Services;

/// <summary>
/// Abstracts all interactions with a digital-wallet provider (GCash, Maya, etc.).
/// This is the only seam between Payment_Service and any wallet provider.
/// Mocked end-to-end for MVP; swappable post-MVP without changing the WebSocket
/// notification flow or PaymentEvent schema.
///
/// Validates: Requirements 3.9
/// </summary>
public interface IWalletAdapter
{
    /// <summary>
    /// Verifies the payment status of a transaction identified by its wallet-provider
    /// transaction reference. Returns the current status as reported by the provider.
    /// </summary>
    /// <param name="walletTransactionId">The wallet provider's unique transaction reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating the payment status and provider metadata.</returns>
    Task<WalletPaymentVerification> VerifyPaymentAsync(
        string walletTransactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the wallet provider for full transaction details by transaction reference.
    /// Used for reconciliation and audit purposes.
    /// </summary>
    /// <param name="walletTransactionId">The wallet provider's unique transaction reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transaction details from the provider, or null if not found.</returns>
    Task<WalletTransactionDetails?> GetTransactionDetailsAsync(
        string walletTransactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the HMAC-SHA256 signature of an incoming webhook payload.
    /// </summary>
    /// <param name="rawBody">The raw request body bytes.</param>
    /// <param name="signature">The signature from the X-Wallet-Signature header.</param>
    /// <param name="timestamp">The timestamp from the X-Wallet-Timestamp header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the signature is valid and the timestamp is within the replay window.</returns>
    Task<bool> ValidateWebhookSignatureAsync(
        byte[] rawBody,
        string signature,
        string timestamp,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of verifying a payment's status with the wallet provider.
/// </summary>
public sealed class WalletPaymentVerification
{
    /// <summary>
    /// The wallet provider's transaction reference.
    /// </summary>
    public required string WalletTransactionId { get; init; }

    /// <summary>
    /// The payment status as reported by the wallet provider.
    /// </summary>
    public required PaymentStatus Status { get; init; }

    /// <summary>
    /// Amount in centavos as confirmed by the provider.
    /// </summary>
    public required int AmountCentavos { get; init; }

    /// <summary>
    /// ISO 4217 currency code confirmed by the provider.
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Timestamp when the provider confirmed the transaction.
    /// </summary>
    public required DateTime VerifiedAt { get; init; }
}

/// <summary>
/// Full transaction details returned by the wallet provider for reconciliation.
/// </summary>
public sealed class WalletTransactionDetails
{
    /// <summary>
    /// The wallet provider's unique transaction reference.
    /// </summary>
    public required string WalletTransactionId { get; init; }

    /// <summary>
    /// Payer identifier as known to the wallet provider.
    /// </summary>
    public required string PayerReference { get; init; }

    /// <summary>
    /// Payee (driver) identifier as known to the wallet provider.
    /// </summary>
    public required string PayeeReference { get; init; }

    /// <summary>
    /// Amount in centavos.
    /// </summary>
    public required int AmountCentavos { get; init; }

    /// <summary>
    /// ISO 4217 currency code.
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// The payment status as reported by the provider.
    /// </summary>
    public required PaymentStatus Status { get; init; }

    /// <summary>
    /// When the transaction was created at the provider.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the transaction was last updated at the provider.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Optional provider-specific metadata (e.g., receipt URL, reference number).
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}
