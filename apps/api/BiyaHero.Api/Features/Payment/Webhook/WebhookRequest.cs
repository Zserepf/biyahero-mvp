namespace BiyaHero.Api.Features.Payment.Webhook;

/// <summary>
/// Deserialized webhook payload from the wallet provider.
/// </summary>
public sealed record WebhookRequest(
    string? EventId,
    string? DriverId,
    string? PayerId,
    string? PayerName,
    string? RouteId,
    int? AmountCentavos,
    string? Currency,
    string? WalletProvider,
    string? WalletTransactionId,
    DateTime? OccurredAt);
