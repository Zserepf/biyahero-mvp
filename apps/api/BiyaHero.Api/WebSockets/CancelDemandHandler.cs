using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.WebSockets;

/// <summary>
/// Handles the WebSocket "cancel-demand" route.
///
/// Flow:
/// 1. Receive the cancel-demand message from the WebSocket route
/// 2. Look up the connection to verify the user is authenticated — 4001 close if not
/// 3. Find the active demand ping for this commuter (via GSI byCommuterId)
/// 4. Delete it immediately from DynamoDB
/// 5. Return success envelope (or no-op if no active ping exists)
///
/// Requirements: 4.5
/// </summary>
public sealed class CancelDemandHandler
{
    private readonly IWsConnectionRepository _wsConnectionRepository;
    private readonly IDemandPingRepository _demandPingRepository;
    private readonly ILogger<CancelDemandHandler> _logger;

    public CancelDemandHandler(
        IWsConnectionRepository wsConnectionRepository,
        IDemandPingRepository demandPingRepository,
        ILogger<CancelDemandHandler> logger)
    {
        _wsConnectionRepository = wsConnectionRepository;
        _demandPingRepository = demandPingRepository;
        _logger = logger;
    }

    /// <summary>
    /// Processes the cancel-demand WebSocket message.
    /// Removes the commuter's active DemandPing immediately from DynamoDB.
    /// </summary>
    /// <param name="connectionId">The API Gateway WebSocket connection ID.</param>
    /// <param name="requestId">The client-generated request ID for correlation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="CancelDemandResult"/> indicating success, no-op, or auth failure.</returns>
    public async Task<CancelDemandResult> HandleAsync(
        string connectionId,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        // Step 2: Look up connection to verify authentication
        var connection = await _wsConnectionRepository.GetConnectionByIdAsync(connectionId, cancellationToken);

        if (connection is null)
        {
            _logger.LogWarning(
                "cancel-demand rejected for connectionId={ConnectionId}: no authenticated connection found",
                connectionId);
            return CancelDemandResult.AuthFailure();
        }

        var commuterId = connection.UserId;

        // Step 3: Find the active demand ping for this commuter via GSI byCommuterId
        var activePing = await _demandPingRepository.GetActivePingByCommuterAsync(commuterId, cancellationToken);

        if (activePing is null)
        {
            _logger.LogInformation(
                "cancel-demand no-op for commuterId={CommuterId}: no active ping found",
                commuterId);
            return CancelDemandResult.NoOp(requestId);
        }

        // Step 4: Delete the ping immediately from DynamoDB
        await _demandPingRepository.DeletePingAsync(
            commuterId,
            activePing.Id,
            activePing.Geohash5,
            cancellationToken);

        _logger.LogInformation(
            "cancel-demand success: removed ping {PingId} for commuterId={CommuterId}",
            activePing.Id, commuterId);

        // Step 5: Return success envelope
        return CancelDemandResult.Success(requestId);
    }
}

/// <summary>
/// Result of the cancel-demand handler.
/// Indicates success (ping deleted), no-op (no active ping), or auth failure (4001 close).
/// </summary>
public sealed record CancelDemandResult
{
    /// <summary>Whether the operation completed successfully (ping deleted or no-op).</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Whether this was a no-op (commuter had no active ping).</summary>
    public bool IsNoOp { get; init; }

    /// <summary>Whether authentication failed (connection not found).</summary>
    public bool IsAuthFailure { get; init; }

    /// <summary>Close code to send on auth failure (4001 per Req 4.7).</summary>
    public int? CloseCode { get; init; }

    /// <summary>Human-readable reason for auth failure.</summary>
    public string? CloseReason { get; init; }

    /// <summary>The client request ID for response correlation.</summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// Creates a successful result — the active ping was deleted.
    /// </summary>
    public static CancelDemandResult Success(string requestId) => new()
    {
        IsSuccess = true,
        IsNoOp = false,
        IsAuthFailure = false,
        RequestId = requestId
    };

    /// <summary>
    /// Creates a no-op result — the commuter had no active ping to cancel.
    /// </summary>
    public static CancelDemandResult NoOp(string requestId) => new()
    {
        IsSuccess = true,
        IsNoOp = true,
        IsAuthFailure = false,
        RequestId = requestId
    };

    /// <summary>
    /// Creates an auth failure result — connection not authenticated, close with 4001.
    /// </summary>
    public static CancelDemandResult AuthFailure() => new()
    {
        IsSuccess = false,
        IsNoOp = false,
        IsAuthFailure = true,
        CloseCode = 4001,
        CloseReason = "Authentication required."
    };
}
