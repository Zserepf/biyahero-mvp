namespace BiyaHero.Api.Features.Payment;

/// <summary>
/// Result of webhook signature verification.
/// </summary>
public sealed class WebhookSignatureResult
{
    public bool IsValid { get; }
    public string? Reason { get; }

    private WebhookSignatureResult(bool isValid, string? reason)
    {
        IsValid = isValid;
        Reason = reason;
    }

    public static WebhookSignatureResult Valid() => new(true, null);
    public static WebhookSignatureResult Invalid(string reason) => new(false, reason);
}
