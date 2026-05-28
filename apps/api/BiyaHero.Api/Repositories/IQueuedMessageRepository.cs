using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Repository interface for queued payment notifications stored in DynamoDB.
/// Messages are enqueued when a driver is offline and drained chronologically
/// when the driver reconnects ($connect).
/// Requirement: 3.6
/// </summary>
public interface IQueuedMessageRepository
{
    /// <summary>
    /// Enqueue a payment notification for an offline driver.
    /// Stores the message with a 24-hour TTL.
    /// </summary>
    /// <param name="driverId">The target driver's ID.</param>
    /// <param name="eventId">The payment event ID.</param>
    /// <param name="occurredAt">When the payment event originally occurred.</param>
    /// <param name="payload">The serialized notification payload (JSON).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnqueueAsync(Guid driverId, string eventId, DateTime occurredAt, string payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drain all queued messages for a driver in chronological order.
    /// Returns the messages sorted by occurredAt, then batch-deletes them from DynamoDB.
    /// Called on driver $connect to deliver missed payment notifications.
    /// </summary>
    /// <param name="driverId">The driver whose messages to drain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Messages in chronological order (oldest first).</returns>
    Task<IReadOnlyList<QueuedMessage>> DrainAsync(Guid driverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count the number of pending queued messages for a driver.
    /// Useful for diagnostics or showing a badge count on reconnect.
    /// </summary>
    /// <param name="driverId">The driver whose messages to count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of pending messages.</returns>
    Task<int> CountAsync(Guid driverId, CancellationToken cancellationToken = default);
}
