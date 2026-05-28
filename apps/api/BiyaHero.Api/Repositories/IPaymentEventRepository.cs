using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Repository interface for PaymentEvent-specific data access in DynamoDB.
/// Provides idempotent writes (conditional attribute_not_exists), single-item
/// lookups by eventId, and driver-history queries via the byDriverId GSI.
///
/// Validates: Requirements 3.1, 3.7, 8.2
/// </summary>
public interface IPaymentEventRepository
{
    /// <summary>
    /// Persist a payment event using a conditional write (attribute_not_exists)
    /// to enforce idempotent webhook processing (Req 3.7).
    /// Returns true if the event was written (new); false if it already existed (duplicate).
    /// </summary>
    Task<bool> PutEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a single payment event by its webhook-supplied event ID.
    /// Returns null if no event exists with the given ID.
    /// </summary>
    Task<PaymentEvent?> GetEventByIdAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query payment events for a specific driver, sorted by occurredAt descending
    /// (most recent first) via the byDriverId GSI. Used for the driver payment dashboard.
    /// </summary>
    Task<IReadOnlyList<PaymentEvent>> GetEventsByDriverAsync(Guid driverId, int limit, CancellationToken cancellationToken = default);
}
