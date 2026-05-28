using BiyaHero.Api.Features.Payment.AudioFailures;

namespace BiyaHero.Api.Tests.Features.Payment;

/// <summary>
/// Unit tests for the AudioFailure endpoint request validation logic.
/// Validates: Requirements 3.8
/// </summary>
public class AudioFailureEndpointTests
{
    // ─── Request Validation: Valid Reasons ─────────────────────────────────

    [Theory]
    [InlineData("muted")]
    [InlineData("autoplay_blocked")]
    [InlineData("voice_unavailable")]
    public void ValidReasons_AcceptsKnownValues(string reason)
    {
        Assert.Contains(reason, AudioFailureRequest.ValidReasons);
    }

    [Theory]
    [InlineData("MUTED")]
    [InlineData("Autoplay_Blocked")]
    [InlineData("VOICE_UNAVAILABLE")]
    public void ValidReasons_IsCaseInsensitive(string reason)
    {
        Assert.Contains(reason, AudioFailureRequest.ValidReasons);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("speaker_broken")]
    [InlineData("network_error")]
    public void ValidReasons_RejectsInvalidValues(string reason)
    {
        Assert.DoesNotContain(reason, AudioFailureRequest.ValidReasons);
    }

    // ─── Request Record Construction ──────────────────────────────────────

    [Fact]
    public void AudioFailureRequest_CanBeConstructedWithAllFields()
    {
        var driverId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var request = new AudioFailureRequest(driverId, eventId, "muted", "Mozilla/5.0");

        Assert.Equal(driverId, request.DriverId);
        Assert.Equal(eventId, request.PaymentEventId);
        Assert.Equal("muted", request.Reason);
        Assert.Equal("Mozilla/5.0", request.UserAgent);
    }

    [Fact]
    public void AudioFailureRequest_AllowsNullOptionalFields()
    {
        var driverId = Guid.NewGuid();

        var request = new AudioFailureRequest(driverId, null, "autoplay_blocked", null);

        Assert.Equal(driverId, request.DriverId);
        Assert.Null(request.PaymentEventId);
        Assert.Equal("autoplay_blocked", request.Reason);
        Assert.Null(request.UserAgent);
    }

    [Fact]
    public void AudioFailureRequest_AllowsNullDriverId()
    {
        // DriverId is nullable to allow validation at the endpoint level (400 for missing)
        var request = new AudioFailureRequest(null, null, "muted", null);

        Assert.Null(request.DriverId);
    }

    [Fact]
    public void AudioFailureRequest_AllowsNullReason()
    {
        // Reason is nullable to allow validation at the endpoint level (400 for missing)
        var request = new AudioFailureRequest(Guid.NewGuid(), null, null, null);

        Assert.Null(request.Reason);
    }

    // ─── ValidReasons Set Completeness ────────────────────────────────────

    [Fact]
    public void ValidReasons_ContainsExactlyThreeValues()
    {
        Assert.Equal(3, AudioFailureRequest.ValidReasons.Count);
    }
}
