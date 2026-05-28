namespace BiyaHero.Api.Features.Payment.Webhook;

/// <summary>
/// Result of webhook handler processing.
/// Encapsulates whether the request was authorized and processed successfully.
/// </summary>
public sealed class WebhookHandlerResult
{
    public bool IsSuccess { get; }
    public bool IsUnauthorized { get; }
    public string? ErrorMessage { get; }

    private WebhookHandlerResult(bool isSuccess, bool isUnauthorized, string? errorMessage)
    {
        IsSuccess = isSuccess;
        IsUnauthorized = isUnauthorized;
        ErrorMessage = errorMessage;
    }

    public static WebhookHandlerResult Success() => new(true, false, null);
    public static WebhookHandlerResult Unauthorized(string reason) => new(false, true, reason);
}
