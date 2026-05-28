using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.WebSockets;

/// <summary>
/// Handles the WebSocket $disconnect event from API Gateway.
///
/// Flow:
/// 1. Receive the connectionId from the API Gateway disconnect event context
/// 2. Look up the connection record via the byConnectionId GSI
/// 3. Delete the connection record from DynamoDB
/// 4. Log the disconnection for observability
/// 5. Always return success — disconnect handlers must never fail
///
/// Requirements: 4.3
/// </summary>
public sealed class DisconnectHandler
{
    private readonly IWsConnectionRepository _wsConnectionRepository;
    private readonly ILogger<DisconnectHandler> _logger;

    public DisconnectHandler(
        IWsConnectionRepository wsConnectionRepository,
        ILogger<DisconnectHandler> logger)
    {
        _wsConnectionRepository = wsConnectionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Processes the $disconnect event. Removes the connection record from DynamoDB
    /// using the byConnectionId GSI for lookup, then deletes the item by its PK/SK.
    /// Always returns success — a disconnect handler should never block the client from closing.
    /// </summary>
    /// <param name="connectionId">The API Gateway WebSocket connection ID being disconnected.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="DisconnectResult"/> indicating the outcome.</returns>
    public async Task<DisconnectResult> HandleAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "WebSocket $disconnect received for connectionId={ConnectionId}",
                connectionId);

            await _wsConnectionRepository.RemoveConnectionAsync(connectionId, cancellationToken);

            _logger.LogInformation(
                "Successfully removed connection record for connectionId={ConnectionId}",
                connectionId);

            return DisconnectResult.Success();
        }
        catch (Exception ex)
        {
            // Disconnect handlers must always succeed — log the error but do not propagate.
            // The connection is closing regardless; failing here would only produce noise.
            _logger.LogError(
                ex,
                "Error removing connection record for connectionId={ConnectionId}. " +
                "The connection will be cleaned up by the 24-hour TTL safety net.",
                connectionId);

            return DisconnectResult.Success();
        }
    }
}

/// <summary>
/// Result of the $disconnect handler. Always indicates success since
/// disconnect handlers must never fail (the connection is already closing).
/// </summary>
public sealed class DisconnectResult
{
    public int StatusCode { get; }

    private DisconnectResult(int statusCode)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Returns a successful disconnect result (HTTP 200).
    /// </summary>
    public static DisconnectResult Success() => new(200);
}
