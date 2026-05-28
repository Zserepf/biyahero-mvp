namespace BiyaHero.Api.Domain;

/// <summary>
/// Lifecycle status of a payment event in the Anti-123 system.
/// </summary>
public enum PaymentStatus
{
    Pending,
    Confirmed,
    Failed,
    Refunded
}
