using System.Security.Cryptography;
using System.Text;
using BiyaHero.Api.Features.Payment;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Tests.Features.Payment;

public class WebhookSignatureVerifierTests
{
    private static readonly byte[] TestSecret = Encoding.UTF8.GetBytes("test-webhook-secret-key-for-hmac");
    private static readonly DateTimeOffset FixedNow = new(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeSecretService _secretService = new();
    private readonly FakeTimeProvider _timeProvider = new(FixedNow);
    private readonly WebhookSignatureVerifier _verifier;

    public WebhookSignatureVerifierTests()
    {
        _verifier = new WebhookSignatureVerifier(_secretService, _timeProvider);
    }

    [Fact]
    public async Task VerifyAsync_ValidSignatureAndTimestamp_ReturnsValid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\",\"amount\":100}");
        var timestamp = FixedNow.ToString("o");
        var signature = ComputeSignatureHex(timestamp, body);

        var result = await _verifier.VerifyAsync(body, signature, timestamp);

        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_MissingSignature_ReturnsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\"}");
        var timestamp = FixedNow.ToString("o");

        var result = await _verifier.VerifyAsync(body, null, timestamp);

        Assert.False(result.IsValid);
        Assert.Contains("Missing X-Wallet-Signature", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_EmptySignature_ReturnsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\"}");
        var timestamp = FixedNow.ToString("o");

        var result = await _verifier.VerifyAsync(body, "", timestamp);

        Assert.False(result.IsValid);
        Assert.Contains("Missing X-Wallet-Signature", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_MissingTimestamp_ReturnsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\"}");
        var timestamp = FixedNow.ToString("o");
        var signature = ComputeSignatureHex(timestamp, body);

        var result = await _verifier.VerifyAsync(body, signature, null);

        Assert.False(result.IsValid);
        Assert.Contains("Missing X-Wallet-Timestamp", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_InvalidTimestampFormat_ReturnsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\"}");
        var timestamp = FixedNow.ToString("o");
        var signature = ComputeSignatureHex(timestamp, body);

        var result = await _verifier.VerifyAsync(body, signature, "not-a-date");

        Assert.False(result.IsValid);
        Assert.Contains("Invalid X-Wallet-Timestamp format", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_TimestampTooOld_ReturnsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\"}");
        var oldTimestamp = FixedNow.AddMinutes(-6).ToString("o");
        var signature = ComputeSignatureHex(oldTimestamp, body);

        var result = await _verifier.VerifyAsync(body, signature, oldTimestamp);

        Assert.False(result.IsValid);
        Assert.Contains("±5 minute tolerance", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_TimestampTooFarInFuture_ReturnsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\"}");
        var futureTimestamp = FixedNow.AddMinutes(6).ToString("o");
        var signature = ComputeSignatureHex(futureTimestamp, body);

        var result = await _verifier.VerifyAsync(body, signature, futureTimestamp);

        Assert.False(result.IsValid);
        Assert.Contains("±5 minute tolerance", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_TimestampAtExactBoundary_ReturnsValid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\"}");
        // Exactly 5 minutes ago — should be within tolerance
        var boundaryTimestamp = FixedNow.AddMinutes(-5).ToString("o");
        var signature = ComputeSignatureHex(boundaryTimestamp, body);

        var result = await _verifier.VerifyAsync(body, signature, boundaryTimestamp);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_WrongSignature_ReturnsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\"}");
        var wrongSignature = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";
        var timestamp = FixedNow.ToString("o");

        var result = await _verifier.VerifyAsync(body, wrongSignature, timestamp);

        Assert.False(result.IsValid);
        Assert.Contains("does not match", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_InvalidHexSignature_ReturnsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\"}");
        var timestamp = FixedNow.ToString("o");

        var result = await _verifier.VerifyAsync(body, "not-valid-hex!", timestamp);

        Assert.False(result.IsValid);
        Assert.Contains("does not match", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_OddLengthHexSignature_ReturnsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\"}");
        var timestamp = FixedNow.ToString("o");

        var result = await _verifier.VerifyAsync(body, "abc", timestamp);

        Assert.False(result.IsValid);
        Assert.Contains("does not match", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_TamperedBody_ReturnsInvalid()
    {
        var originalBody = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\",\"amount\":100}");
        var timestamp = FixedNow.ToString("o");
        var signature = ComputeSignatureHex(timestamp, originalBody);
        var tamperedBody = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\",\"amount\":999}");

        var result = await _verifier.VerifyAsync(tamperedBody, signature, timestamp);

        Assert.False(result.IsValid);
        Assert.Contains("does not match", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_ValidBase64Signature_ReturnsValid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-456\",\"amount\":250}");
        var timestamp = FixedNow.ToString("o");
        var signature = ComputeSignatureBase64(timestamp, body);

        var result = await _verifier.VerifyAsync(body, signature, timestamp);

        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_InvalidBase64Signature_ReturnsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-456\"}");
        // Valid base64 but wrong HMAC value
        var wrongBase64 = Convert.ToBase64String(new byte[32]);
        var timestamp = FixedNow.ToString("o");

        var result = await _verifier.VerifyAsync(body, wrongBase64, timestamp);

        Assert.False(result.IsValid);
        Assert.Contains("does not match", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_UppercaseHexSignature_ReturnsValid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-789\"}");
        var timestamp = FixedNow.ToString("o");
        var signedPayload = BuildTestPayload(timestamp, body);
        var hash = HMACSHA256.HashData(TestSecret, signedPayload);
        var signature = Convert.ToHexString(hash); // uppercase hex

        var result = await _verifier.VerifyAsync(body, signature, timestamp);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_UsesConstantTimeComparison_DoesNotShortCircuit()
    {
        // This test verifies that invalid signatures of different lengths/content
        // all produce the same result type (no timing leak via different error paths).
        // The implementation uses CryptographicOperations.FixedTimeEquals which is
        // the .NET standard constant-time comparison primitive.
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-timing\"}");
        var timestamp = FixedNow.ToString("o");
        var signedPayload = BuildTestPayload(timestamp, body);

        // Signature with first byte wrong
        var correctHash = HMACSHA256.HashData(TestSecret, signedPayload);
        correctHash[0] ^= 0xFF; // flip first byte
        var sig1 = Convert.ToHexString(correctHash).ToLowerInvariant();

        // Signature with last byte wrong
        var correctHash2 = HMACSHA256.HashData(TestSecret, signedPayload);
        correctHash2[^1] ^= 0xFF; // flip last byte
        var sig2 = Convert.ToHexString(correctHash2).ToLowerInvariant();

        var result1 = await _verifier.VerifyAsync(body, sig1, timestamp);
        var result2 = await _verifier.VerifyAsync(body, sig2, timestamp);

        // Both should fail with the same error message (no information leak)
        Assert.False(result1.IsValid);
        Assert.False(result2.IsValid);
        Assert.Equal(result1.Reason, result2.Reason);
    }

    [Fact]
    public async Task VerifyAsync_EmptyBody_WithValidSignature_ReturnsValid()
    {
        var body = Array.Empty<byte>();
        var timestamp = FixedNow.ToString("o");
        var signature = ComputeSignatureHex(timestamp, body);

        var result = await _verifier.VerifyAsync(body, signature, timestamp);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_WhitespaceOnlySignature_ReturnsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\"}");
        var timestamp = FixedNow.ToString("o");

        var result = await _verifier.VerifyAsync(body, "   ", timestamp);

        Assert.False(result.IsValid);
        Assert.Contains("Missing X-Wallet-Signature", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_WhitespaceOnlyTimestamp_ReturnsInvalid()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"evt-123\"}");
        var timestamp = FixedNow.ToString("o");
        var signature = ComputeSignatureHex(timestamp, body);

        var result = await _verifier.VerifyAsync(body, signature, "   ");

        Assert.False(result.IsValid);
        Assert.Contains("Missing X-Wallet-Timestamp", result.Reason);
    }

    private static string ComputeSignatureHex(string timestamp, byte[] body)
    {
        var signedPayload = BuildTestPayload(timestamp, body);
        var hash = HMACSHA256.HashData(TestSecret, signedPayload);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeSignatureBase64(string timestamp, byte[] body)
    {
        var signedPayload = BuildTestPayload(timestamp, body);
        var hash = HMACSHA256.HashData(TestSecret, signedPayload);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Builds the signed payload as: timestamp + "." + rawBody (matching the verifier's logic).
    /// </summary>
    private static byte[] BuildTestPayload(string timestamp, byte[] body)
    {
        var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        var payload = new byte[prefix.Length + body.Length];
        Buffer.BlockCopy(prefix, 0, payload, 0, prefix.Length);
        Buffer.BlockCopy(body, 0, payload, prefix.Length, body.Length);
        return payload;
    }

    private sealed class FakeSecretService : ISecretService
    {
        public Task<byte[]> GetJwtSigningKeyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Encoding.UTF8.GetBytes("jwt-key"));

        public Task<byte[]> GetWebhookSigningSecretAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(TestSecret);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
