namespace BiyaHero.Api.Features.Payment.AudioFailures;

/// <summary>
/// Request body for POST /v1/payments/audio-failures.
/// Reports driver-side audio playback failures (muted device, autoplay blocked)
/// for CloudWatch observability.
///
/// Valid Reason values: "muted", "autoplay_blocked", "voice_unavailable".
/// Requirements: 3.8
/// </summary>
public sealed record AudioFailureRequest(
    Guid? DriverId,
    Guid? PaymentEventId,
    string? Reason,
    string? UserAgent)
{
    /// <summary>
    /// The set of accepted failure reason values.
    /// </summary>
    public static readonly HashSet<string> ValidReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "muted",
        "autoplay_blocked",
        "voice_unavailable"
    };
}
