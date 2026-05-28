using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Services;

/// <summary>
/// Mock implementation of IWalletAdapter for MVP testing.
/// Returns hardcoded/simulated responses that mimic a real wallet provider (GCash, Maya).
/// Swappable post-MVP with a real provider implementation without changing the
/// WebSocket notification flow or PaymentEvent schema.
///
/// Validates: Requirements 3.9
/// </summary>
public sealed class MockWalletAdapter : IWalletAdapter
{
    private readonly ISecretService _secretService;
    private static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(5);

    public MockWalletAdapter(ISecretService secretService)
    {
        _secretService = secretService;
    }

    /// <inheritdoc />
    public Task<WalletPaymentVerification> VerifyPaymentAsync(
        string walletTransactionId,
        CancellationToken cancellationToken = default)
    {
        // Mock: always returns Confirmed status for any transaction ID
        var verification = new WalletPaymentVerification
        {
            WalletTransactionId = walletTransactionId,
            Status = PaymentStatus.Confirmed,
            AmountCentavos = 1300, // simulated 13 PHP fare
            Currency = "PHP",
            VerifiedAt = DateTime.UtcNow
        };

        return Task.FromResult(verification);
    }

    /// <inheritdoc />
    public Task<WalletTransactionDetails?> GetTransactionDetailsAsync(
        string walletTransactionId,
        CancellationToken cancellationToken = default)
    {
        // Mock: returns simulated transaction details for any transaction ID
        var details = new WalletTransactionDetails
        {
            WalletTransactionId = walletTransactionId,
            PayerReference = "mock-payer-ref-001",
            PayeeReference = "mock-payee-ref-001",
            AmountCentavos = 1300,
            Currency = "PHP",
            Status = PaymentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddSeconds(-30),
            UpdatedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                ["provider"] = "mock",
                ["receiptUrl"] = $"https://mock-wallet.example/receipts/{walletTransactionId}"
            }
        };

        return Task.FromResult<WalletTransactionDetails?>(details);
    }

    /// <inheritdoc />
    public async Task<bool> ValidateWebhookSignatureAsync(
        byte[] rawBody,
        string signature,
        string timestamp,
        CancellationToken cancellationToken = default)
    {
        // Validate timestamp is within the ±5 minute replay window
        if (!DateTime.TryParse(timestamp, out var webhookTimestamp))
        {
            return false;
        }

        var drift = DateTime.UtcNow - webhookTimestamp.ToUniversalTime();
        if (Math.Abs(drift.TotalMinutes) > ReplayWindow.TotalMinutes)
        {
            return false;
        }

        // Compute expected HMAC-SHA256(secret, timestamp + "." + rawBody) per design spec
        var secret = await _secretService.GetWebhookSigningSecretAsync(cancellationToken);
        var prefix = System.Text.Encoding.UTF8.GetBytes(timestamp + ".");
        var signedPayload = new byte[prefix.Length + rawBody.Length];
        Buffer.BlockCopy(prefix, 0, signedPayload, 0, prefix.Length);
        Buffer.BlockCopy(rawBody, 0, signedPayload, prefix.Length, rawBody.Length);

        using var hmac = new System.Security.Cryptography.HMACSHA256(secret);
        var computedHash = hmac.ComputeHash(signedPayload);
        var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

        // Constant-time comparison to prevent timing attacks
        return CryptographicEquals(computedSignature, signature.ToLowerInvariant());
    }

    /// <summary>
    /// Constant-time string comparison to prevent timing side-channel attacks.
    /// </summary>
    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }
}
