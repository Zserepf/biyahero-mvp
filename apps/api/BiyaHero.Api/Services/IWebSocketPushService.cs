namespace BiyaHero.Api.Services;

/// <summary>
/// Abstraction over AWS API Gateway Management API's PostToConnection.
/// Pushes messages to connected WebSocket clients.
/// This is the only layer that touches the API Gateway Management SDK.
/// </summary>
public interface IWebSocketPushService
{
    /// <summary>
    /// Push a JSON payload to a specific WebSocket connection.
    /// Returns true if the message was delivered successfully.
    /// Returns false if the connection is stale/gone (410 GoneException).
    /// </summary>
    /// <param name="connectionId">The API Gateway WebSocket connection ID.</param>
    /// <param name="payload">The JSON string payload to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> PostToConnectionAsync(string connectionId, string payload, CancellationToken cancellationToken = default);
}
