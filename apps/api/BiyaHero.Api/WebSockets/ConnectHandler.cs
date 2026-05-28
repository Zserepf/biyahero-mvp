using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.WebSockets;

/// <summary>
/// Handles the WebSocket $connect handshake.
///
/// Flow:
/// 1. Extract JWT from query string parameter "token"
/// 2. Validate the JWT — if missing/expired/invalid: close with code 4001 and force-close
/// 3. On valid JWT: register WsConnection in DynamoDB (userId, connectionId, role, connectedAt, 24h TTL)
/// 4. Drain any QueuedMessages for this user and push them to the new connection
/// 5. Return success to allow the connection
///
/// Requirements: 3.6, 4.7, 5.4
/// </summary>
public sealed class ConnectHandler
{
    private const int AuthFailureCloseCode = 4001;
    private const string AuthFailureCloseReason = "Authentication failed.";
    private const int TtlHours = 24;

    private readonly IJwtService _jwtService;
    private readonly IWsConnectionRepository _wsConnectionRepository;
    private readonly IQueuedMessageRepository _queuedMessageRepository;
    private readonly IWebSocketPushService _webSocketPushService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConnectHandler> _logger;

    public ConnectHandler(
        IJwtService jwtService,
        IWsConnectionRepository wsConnectionRepository,
        IQueuedMessageRepository queuedMessageRepository,
        IWebSocketPushService webSocketPushService,
        TimeProvider timeProvider,
        ILogger<ConnectHandler> logger)
    {
        _jwtService = jwtService;
        _wsConnectionRepository = wsConnectionRepository;
        _queuedMessageRepository = queuedMessageRepository;
        _webSocketPushService = webSocketPushService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Processes the $connect event for a WebSocket connection.
    /// </summary>
    /// <param name="connectionId">The API Gateway WebSocket connection ID.</param>
    /// <param name="token">The JWT token from the query string "token" parameter. Null if missing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ConnectResult indicating success or auth failure.</returns>
    public async Task<ConnectResult> HandleAsync(
        string connectionId,
        string? token,
        CancellationToken cancellationToken = default)
    {
        // Step 1 & 2: Validate JWT — reject with 4001 if missing/expired/invalid
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning(
                "WebSocket $connect rejected for connection {ConnectionId}: missing token",
                connectionId);
            return ConnectResult.AuthFailure("Missing authentication token.");
        }

        var validationResult = await _jwtService.ValidateTokenDetailedAsync(token, cancellationToken);

        if (!validationResult.IsValid || validationResult.UserId is null)
        {
            _logger.LogWarning(
                "WebSocket $connect rejected for connection {ConnectionId}: {Reason}",
                connectionId, validationResult.ErrorMessage ?? "Invalid token");
            return ConnectResult.AuthFailure(validationResult.ErrorMessage ?? "Invalid or expired token.");
        }

        var userId = validationResult.UserId.Value;
        var role = ParseRole(validationResult.Role);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Step 3: Register WsConnection in DynamoDB
        var connection = new WsConnection(
            id: Guid.NewGuid(),
            createdAt: now,
            updatedAt: now,
            userId: userId,
            role: role,
            connectionId: connectionId,
            connectedAt: now,
            subscribedBbox: null,
            expiresAt: now.AddHours(TtlHours));

        await _wsConnectionRepository.RegisterConnectionAsync(connection, cancellationToken);

        _logger.LogInformation(
            "WebSocket $connect registered: userId={UserId}, connectionId={ConnectionId}, role={Role}",
            userId, connectionId, role);

        // Step 4: Drain QueuedMessages for this user and push to the new connection
        await DrainQueuedMessagesAsync(userId, connectionId, cancellationToken);

        // Step 5: Return success
        return ConnectResult.Ok(userId, role);
    }

    /// <summary>
    /// Drains queued messages for the user and pushes them to the newly connected WebSocket.
    /// Messages are delivered in chronological order (occurredAt).
    /// Failed deliveries are logged but do not block the connection.
    /// Requirement: 3.6
    /// </summary>
    private async Task DrainQueuedMessagesAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<QueuedMessage> messages;
        try
        {
            messages = await _queuedMessageRepository.DrainAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't fail the connection if queue drain fails — log and continue
            _logger.LogError(ex,
                "Failed to drain queued messages for userId={UserId} on connection {ConnectionId}",
                userId, connectionId);
            return;
        }

        if (messages.Count == 0)
            return;

        _logger.LogInformation(
            "Draining {Count} queued message(s) for userId={UserId} on connection {ConnectionId}",
            messages.Count, userId, connectionId);

        foreach (var message in messages)
        {
            try
            {
                var success = await _webSocketPushService.PostToConnectionAsync(
                    connectionId, message.Payload, cancellationToken);

                if (!success)
                {
                    _logger.LogWarning(
                        "Failed to deliver queued message eventId={EventId} to connection {ConnectionId} — connection may have closed",
                        message.EventId, connectionId);
                    // Connection gone during drain — remaining messages are lost for this session.
                    // They were already deleted by DrainAsync. This is acceptable per the design:
                    // the drain is best-effort on the new connection.
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error delivering queued message eventId={EventId} to connection {ConnectionId}",
                    message.EventId, connectionId);
                break;
            }
        }
    }

    /// <summary>
    /// Parses the role string from JWT claims into a UserRole enum value.
    /// Defaults to Commuter if the role is unrecognized.
    /// </summary>
    private static UserRole ParseRole(string? roleString)
    {
        if (string.IsNullOrWhiteSpace(roleString))
            return UserRole.Commuter;

        return Enum.TryParse<UserRole>(roleString, ignoreCase: true, out var role)
            ? role
            : UserRole.Commuter;
    }
}
