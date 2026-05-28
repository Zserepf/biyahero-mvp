using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Repository interface for WebSocket connection tracking in DynamoDB.
/// Supports connection lifecycle (register/remove), user-based lookups,
/// heatmap bbox subscription updates, and fan-out queries.
/// Requirements: 3.2, 3.6, 4.3, 5.4
/// </summary>
public interface IWsConnectionRepository
{
    /// <summary>
    /// Register a new WebSocket connection in DynamoDB.
    /// Called on $connect after JWT validation succeeds.
    /// </summary>
    Task RegisterConnectionAsync(WsConnection connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a WebSocket connection by its connectionId.
    /// Queries the byConnectionId GSI to find the owning user's PK, then deletes.
    /// Called on $disconnect.
    /// </summary>
    Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active connections for a given user.
    /// Used by Payment_Service to check if a driver is online (Req 3.2).
    /// </summary>
    Task<IReadOnlyList<WsConnection>> GetConnectionsByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single connection by its connectionId via the byConnectionId GSI.
    /// Returns null if the connection does not exist.
    /// </summary>
    Task<WsConnection?> GetConnectionByIdAsync(string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the heatmap bounding-box subscription for a connection.
    /// Pass null to unsubscribe from heatmap deltas.
    /// Called when a driver sends subscribe-heatmap (Req 4.3).
    /// </summary>
    Task UpdateSubscriptionAsync(string connectionId, string? bbox, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all connections that have an active heatmap bbox subscription.
    /// Used by the heatmap aggregator to fan out delta pushes to subscribed drivers (Req 4.3).
    /// </summary>
    Task<IReadOnlyList<WsConnection>> GetSubscribedConnectionsAsync(CancellationToken cancellationToken = default);
}
