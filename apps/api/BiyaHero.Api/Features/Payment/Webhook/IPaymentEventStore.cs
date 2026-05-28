using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Features.Payment.Webhook;

/// <summary>
/// In-memory store for PaymentEvents (MVP).
/// Replaceable with DynamoDB-backed repository post-MVP.
/// </summary>
public interface IPaymentEventStore
{
    /// <summary>
    /// Checks if a payment event with the given eventId already exists (idempotence check).
    /// </summary>
    bool Exists(string eventId);

    /// <summary>
    /// Stores a payment event keyed by eventId.
    /// </summary>
    void Add(string eventId, PaymentEvent paymentEvent);

    /// <summary>
    /// Retrieves a payment event by eventId. Returns null if not found.
    /// </summary>
    PaymentEvent? Get(string eventId);
}
