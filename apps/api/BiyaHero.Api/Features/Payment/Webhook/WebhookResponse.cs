namespace BiyaHero.Api.Features.Payment.Webhook;

/// <summary>
/// Response body for a successful webhook processing.
/// </summary>
public sealed record WebhookResponse(bool Received);
