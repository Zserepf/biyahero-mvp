using System.Security.Cryptography;
using System.Text;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Payment;

/// <summary>
/// HMAC-SHA256 webhook signature verifier.
/// Validates X-Wallet-Signature over "timestamp.rawBody" using a KMS-stored secret
/// and checks X-Wallet-Timestamp within ±5 minutes to block replay attacks.
///
/// Signature formula: HMAC-SHA256(secret, timestamp + "." + rawBody)
/// This binds the timestamp to the signature, preventing replay with a different timestamp.
///
/// Requirements: 3.5
/// </summary>
public sealed class WebhookSignatureVerifier : IWebhookSignatureVerifier
{
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromMinutes(5);

    private readonly ISecretService _secretService;
    private readonly TimeProvider _timeProvider;

    public WebhookSignatureVerifier(ISecretService secretService, TimeProvider timeProvider)
    {
        _secretService = secretService;
        _timeProvider = timeProvider;
    }

    public async Task<WebhookSignatureResult> VerifyAsync(
        byte[] rawBody,
        string? signature,
        string? timestamp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return WebhookSignatureResult.Invalid("Missing X-Wallet-Signature header.");
        }

        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return WebhookSignatureResult.Invalid("Missing X-Wallet-Timestamp header.");
        }

        // Validate timestamp format and replay window
        if (!DateTimeOffset.TryParse(timestamp, out var parsedTimestamp))
        {
            return WebhookSignatureResult.Invalid("Invalid X-Wallet-Timestamp format.");
        }

        var now = _timeProvider.GetUtcNow();
        var drift = now - parsedTimestamp;

        if (drift.Duration() > TimestampTolerance)
        {
            return WebhookSignatureResult.Invalid("X-Wallet-Timestamp is outside the ±5 minute tolerance window.");
        }

        // Compute HMAC-SHA256(secret, timestamp + "." + rawBody) per design spec.
        // Including the timestamp in the signed payload prevents replay with a different timestamp.
        var secret = await _secretService.GetWebhookSigningSecretAsync(cancellationToken);
        var signedPayload = BuildSignedPayload(timestamp, rawBody);
        var expectedSignature = ComputeHmacSha256(secret, signedPayload);

        // Constant-time comparison to prevent timing attacks.
        // Supports both hex-encoded and base64-encoded signatures.
        var providedBytes = DecodeSignature(signature);
        if (providedBytes is null || !CryptographicOperations.FixedTimeEquals(expectedSignature, providedBytes))
        {
            return WebhookSignatureResult.Invalid("X-Wallet-Signature does not match the expected HMAC-SHA256.");
        }

        return WebhookSignatureResult.Valid();
    }

    /// <summary>
    /// Builds the signed payload as: timestamp + "." + rawBody (UTF-8 bytes).
    /// This ensures the timestamp is cryptographically bound to the body.
    /// </summary>
    private static byte[] BuildSignedPayload(string timestamp, byte[] rawBody)
    {
        var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        var payload = new byte[prefix.Length + rawBody.Length];
        Buffer.BlockCopy(prefix, 0, payload, 0, prefix.Length);
        Buffer.BlockCopy(rawBody, 0, payload, prefix.Length, rawBody.Length);
        return payload;
    }

    private static byte[] ComputeHmacSha256(byte[] key, byte[] data)
    {
        return HMACSHA256.HashData(key, data);
    }

    /// <summary>
    /// Attempts to decode the signature as hex first, then falls back to base64.
    /// Returns null if neither decoding succeeds.
    /// </summary>
    private static byte[]? DecodeSignature(string signature)
    {
        // Try hex first (64 hex chars = 32 bytes for SHA-256)
        var hexBytes = ConvertHexToBytes(signature);
        if (hexBytes is not null)
        {
            return hexBytes;
        }

        // Fall back to base64
        return ConvertBase64ToBytes(signature);
    }

    private static byte[]? ConvertBase64ToBytes(string base64)
    {
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static byte[]? ConvertHexToBytes(string hex)
    {
        if (hex.Length % 2 != 0)
        {
            return null;
        }

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var highNibble = HexCharToNibble(hex[i * 2]);
            var lowNibble = HexCharToNibble(hex[i * 2 + 1]);

            if (highNibble < 0 || lowNibble < 0)
            {
                return null;
            }

            bytes[i] = (byte)((highNibble << 4) | lowNibble);
        }

        return bytes;
    }

    private static int HexCharToNibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };
}
