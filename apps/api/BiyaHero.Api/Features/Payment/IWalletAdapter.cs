namespace BiyaHero.Api.Features.Payment;

/// <summary>
/// Defines the contract for wallet provider interactions.
/// This is the only seam between Payment_Service and any digital-wallet provider.
/// Mocked end-to-end for MVP; swappable post-MVP without changing the WebSocket
/// notification flow or PaymentEvent schema.
/// </summary>
public interface IWalletAdapter
{
    /// <summary>
    /// Transfers funds from one user's wallet to another.
    /// </summary>
    /// <param name="fromUserId">The payer's user ID.</param>
    /// <param name="toUserId">The recipient's user ID (typically the driver).</param>
    /// <param name="amountCentavos">The transfer amount in centavos (PHP).</param>
    /// <param name="currency">The currency code (e.g., "PHP").</param>
    /// <param name="idempotencyKey">A unique key to ensure the transfer is processed at most once.</param>
    /// <returns>A <see cref="WalletTransferResult"/> indicating the outcome.</returns>
    Task<WalletTransferResult> TransferAsync(
        Guid fromUserId,
        Guid toUserId,
        int amountCentavos,
        string currency,
        string idempotencyKey);

    /// <summary>
    /// Retrieves the current wallet balance for a user.
    /// </summary>
    /// <param name="userId">The user whose balance to retrieve.</param>
    /// <param name="currency">The currency code (e.g., "PHP").</param>
    /// <returns>A <see cref="WalletBalanceResult"/> with the current balance.</returns>
    Task<WalletBalanceResult> GetBalanceAsync(Guid userId, string currency);
}

/// <summary>
/// Result of a wallet transfer operation.
/// </summary>
/// <param name="Success">Whether the transfer completed successfully.</param>
/// <param name="TransactionId">Provider-assigned transaction identifier (null on failure).</param>
/// <param name="ErrorMessage">Human-readable error description (null on success).</param>
public sealed record WalletTransferResult(
    bool Success,
    string? TransactionId,
    string? ErrorMessage)
{
    public static WalletTransferResult Succeeded(string transactionId) =>
        new(true, transactionId, null);

    public static WalletTransferResult Failed(string errorMessage) =>
        new(false, null, errorMessage);
}

/// <summary>
/// Result of a wallet balance query.
/// </summary>
/// <param name="BalanceCentavos">The current balance in centavos.</param>
/// <param name="Currency">The currency code (e.g., "PHP").</param>
public sealed record WalletBalanceResult(int BalanceCentavos, string Currency);
