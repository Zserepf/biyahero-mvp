namespace BiyaHero.Api.Features.Payment;

/// <summary>
/// Mock wallet adapter for development and testing.
/// Always succeeds with a generated transaction ID and simulates a 100ms network delay.
/// Swap with a real provider implementation post-MVP without changing the
/// WebSocket notification flow or PaymentEvent schema.
/// </summary>
public sealed class MockWalletAdapter : IWalletAdapter
{
    private static readonly TimeSpan SimulatedDelay = TimeSpan.FromMilliseconds(100);

    /// <inheritdoc />
    public async Task<WalletTransferResult> TransferAsync(
        Guid fromUserId,
        Guid toUserId,
        int amountCentavos,
        string currency,
        string idempotencyKey)
    {
        await Task.Delay(SimulatedDelay);

        var transactionId = $"mock-txn-{Guid.NewGuid():N}";
        return WalletTransferResult.Succeeded(transactionId);
    }

    /// <inheritdoc />
    public async Task<WalletBalanceResult> GetBalanceAsync(Guid userId, string currency)
    {
        await Task.Delay(SimulatedDelay);

        // Mock always returns a generous balance for development/testing
        return new WalletBalanceResult(BalanceCentavos: 1_000_000, Currency: currency);
    }
}
