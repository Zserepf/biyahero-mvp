using System.Security.Claims;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;
using BiyaHero.Api.WebSockets;
using Microsoft.Extensions.Logging.Abstractions;

namespace BiyaHero.Api.Tests.WebSockets;

/// <summary>
/// Unit tests for the WebSocket $connect handler.
/// Verifies JWT validation, WsConnection registration, queued message drain,
/// and 4001 close on auth failure.
/// Requirements: 3.6, 4.7, 5.4
/// </summary>
public class ConnectHandlerTests
{
    private readonly FakeJwtService _jwtService = new();
    private readonly FakeWsConnectionRepository _wsConnectionRepository = new();
    private readonly FakeQueuedMessageRepository _queuedMessageRepository = new();
    private readonly FakeWebSocketPushService _webSocketPushService = new();
    private readonly ConnectHandler _handler;

    public ConnectHandlerTests()
    {
        _handler = new ConnectHandler(
            _jwtService,
            _wsConnectionRepository,
            _queuedMessageRepository,
            _webSocketPushService,
            TimeProvider.System,
            NullLogger<ConnectHandler>.Instance);
    }

    // ─── Auth Validation Tests ────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_MissingToken_ReturnsAuthFailureWith4001()
    {
        // Act
        var result = await _handler.HandleAsync("conn-123", token: null);

        // Assert
        Assert.False(result.IsAccepted);
        Assert.Equal(4001, result.CloseCode);
        Assert.Contains("Missing", result.CloseReason);
    }

    [Fact]
    public async Task HandleAsync_EmptyToken_ReturnsAuthFailureWith4001()
    {
        // Act
        var result = await _handler.HandleAsync("conn-123", token: "");

        // Assert
        Assert.False(result.IsAccepted);
        Assert.Equal(4001, result.CloseCode);
    }

    [Fact]
    public async Task HandleAsync_WhitespaceToken_ReturnsAuthFailureWith4001()
    {
        // Act
        var result = await _handler.HandleAsync("conn-123", token: "   ");

        // Assert
        Assert.False(result.IsAccepted);
        Assert.Equal(4001, result.CloseCode);
    }

    [Fact]
    public async Task HandleAsync_ExpiredToken_ReturnsAuthFailureWith4001()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Failure("Token expired."));

        // Act
        var result = await _handler.HandleAsync("conn-123", token: "expired-jwt");

        // Assert
        Assert.False(result.IsAccepted);
        Assert.Equal(4001, result.CloseCode);
        Assert.NotNull(result.CloseReason);
    }

    [Fact]
    public async Task HandleAsync_InvalidToken_ReturnsAuthFailureWith4001()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Failure("Invalid signature."));

        // Act
        var result = await _handler.HandleAsync("conn-123", token: "bad-jwt");

        // Assert
        Assert.False(result.IsAccepted);
        Assert.Equal(4001, result.CloseCode);
    }

    // ─── Successful Connection Tests ──────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidToken_ReturnsAccepted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, "driver@test.com", "Driver"));

        // Act
        var result = await _handler.HandleAsync("conn-456", token: "valid-jwt");

        // Assert
        Assert.True(result.IsAccepted);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(UserRole.Driver, result.Role);
    }

    [Fact]
    public async Task HandleAsync_ValidToken_RegistersWsConnection()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, "commuter@test.com", "Commuter"));

        // Act
        await _handler.HandleAsync("conn-789", token: "valid-jwt");

        // Assert
        Assert.Single(_wsConnectionRepository.RegisteredConnections);
        var registered = _wsConnectionRepository.RegisteredConnections[0];
        Assert.Equal(userId, registered.UserId);
        Assert.Equal("conn-789", registered.ConnectionId);
        Assert.Equal(UserRole.Commuter, registered.Role);
        Assert.True(registered.ExpiresAt > DateTime.UtcNow.AddHours(23));
    }

    [Fact]
    public async Task HandleAsync_ValidToken_SuperAdminRole_RegistersCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, "admin@test.com", "SuperAdmin"));

        // Act
        var result = await _handler.HandleAsync("conn-admin", token: "valid-jwt");

        // Assert
        Assert.True(result.IsAccepted);
        Assert.Equal(UserRole.SuperAdmin, result.Role);
        Assert.Single(_wsConnectionRepository.RegisteredConnections);
        Assert.Equal(UserRole.SuperAdmin, _wsConnectionRepository.RegisteredConnections[0].Role);
    }

    // ─── Queue Drain Tests ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidToken_NoQueuedMessages_DoesNotPush()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, "driver@test.com", "Driver"));
        // No queued messages for this user

        // Act
        await _handler.HandleAsync("conn-drain-empty", token: "valid-jwt");

        // Assert
        Assert.Empty(_webSocketPushService.PushedMessages);
    }

    [Fact]
    public async Task HandleAsync_ValidToken_WithQueuedMessages_DrainsAndPushes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, "driver@test.com", "Driver"));

        var msg1 = CreateQueuedMessage(userId, "evt-1", DateTime.UtcNow.AddMinutes(-10), "{\"action\":\"payment.confirmed\",\"data\":{\"amount\":100}}");
        var msg2 = CreateQueuedMessage(userId, "evt-2", DateTime.UtcNow.AddMinutes(-5), "{\"action\":\"payment.confirmed\",\"data\":{\"amount\":200}}");
        _queuedMessageRepository.SetMessages(userId, new[] { msg1, msg2 });

        // Act
        await _handler.HandleAsync("conn-drain", token: "valid-jwt");

        // Assert — both messages pushed to the new connection
        Assert.Equal(2, _webSocketPushService.PushedMessages.Count);
        Assert.All(_webSocketPushService.PushedMessages, m => Assert.Equal("conn-drain", m.ConnectionId));
        Assert.Equal(msg1.Payload, _webSocketPushService.PushedMessages[0].Payload);
        Assert.Equal(msg2.Payload, _webSocketPushService.PushedMessages[1].Payload);
    }

    [Fact]
    public async Task HandleAsync_ValidToken_QueueDrainFails_StillReturnsAccepted()
    {
        // Arrange — drain will throw, but connection should still succeed
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, "driver@test.com", "Driver"));
        _queuedMessageRepository.ThrowOnDrain = true;

        // Act
        var result = await _handler.HandleAsync("conn-drain-error", token: "valid-jwt");

        // Assert — connection accepted despite drain failure
        Assert.True(result.IsAccepted);
        Assert.Single(_wsConnectionRepository.RegisteredConnections);
    }

    [Fact]
    public async Task HandleAsync_ValidToken_PushFails_StopsDelivery()
    {
        // Arrange — push will fail, simulating a connection that closed during drain
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, "driver@test.com", "Driver"));

        var msg1 = CreateQueuedMessage(userId, "evt-1", DateTime.UtcNow.AddMinutes(-10), "payload-1");
        var msg2 = CreateQueuedMessage(userId, "evt-2", DateTime.UtcNow.AddMinutes(-5), "payload-2");
        _queuedMessageRepository.SetMessages(userId, new[] { msg1, msg2 });
        _webSocketPushService.FailOnPush = true;

        // Act
        var result = await _handler.HandleAsync("conn-push-fail", token: "valid-jwt");

        // Assert — connection still accepted, but only first push attempted before break
        Assert.True(result.IsAccepted);
        Assert.Single(_webSocketPushService.PushedMessages);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static QueuedMessage CreateQueuedMessage(Guid driverId, string eventId, DateTime occurredAt, string payload)
    {
        return new QueuedMessage(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            driverId: driverId,
            eventId: eventId,
            occurredAt: occurredAt,
            payload: payload,
            expiresAt: DateTime.UtcNow.AddHours(24));
    }

    // ─── Fakes ────────────────────────────────────────────────────────────

    private sealed class FakeJwtService : IJwtService
    {
        private JwtValidationResult _validationResult = JwtValidationResult.Failure("Not configured.");

        public void SetValidationResult(JwtValidationResult result) => _validationResult = result;

        public Task<string> GenerateAccessTokenAsync(User user, CancellationToken cancellationToken = default)
            => Task.FromResult("fake-access-token");

        public Task<string> GenerateRefreshTokenAsync(User user, CancellationToken cancellationToken = default)
            => Task.FromResult("fake-refresh-token");

        public Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (_validationResult.IsValid)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, _validationResult.UserId!.Value.ToString()),
                    new(ClaimTypes.Email, _validationResult.Email ?? ""),
                    new(ClaimTypes.Role, _validationResult.Role ?? "Commuter")
                };
                var identity = new ClaimsIdentity(claims, "Bearer");
                return Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(identity));
            }
            return Task.FromResult<ClaimsPrincipal?>(null);
        }

        public Task<Guid?> GetUserIdFromTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(_validationResult.UserId);

        public Task<JwtValidationResult> ValidateTokenDetailedAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(_validationResult);
    }

    private sealed class FakeWsConnectionRepository : IWsConnectionRepository
    {
        public List<WsConnection> RegisteredConnections { get; } = new();

        public Task RegisterConnectionAsync(WsConnection connection, CancellationToken cancellationToken = default)
        {
            RegisteredConnections.Add(connection);
            return Task.CompletedTask;
        }

        public Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WsConnection>> GetConnectionsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(Array.Empty<WsConnection>());

        public Task<WsConnection?> GetConnectionByIdAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.FromResult<WsConnection?>(null);

        public Task UpdateSubscriptionAsync(string connectionId, string? bbox, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WsConnection>> GetSubscribedConnectionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(Array.Empty<WsConnection>());
    }

    private sealed class FakeQueuedMessageRepository : IQueuedMessageRepository
    {
        private readonly Dictionary<Guid, List<QueuedMessage>> _messages = new();
        public bool ThrowOnDrain { get; set; }

        public void SetMessages(Guid driverId, IEnumerable<QueuedMessage> messages)
        {
            _messages[driverId] = messages.ToList();
        }

        public Task EnqueueAsync(Guid driverId, string eventId, DateTime occurredAt, string payload, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<QueuedMessage>> DrainAsync(Guid driverId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnDrain)
                throw new InvalidOperationException("Simulated DynamoDB failure");

            if (_messages.TryGetValue(driverId, out var msgs))
            {
                _messages.Remove(driverId);
                return Task.FromResult<IReadOnlyList<QueuedMessage>>(msgs);
            }
            return Task.FromResult<IReadOnlyList<QueuedMessage>>(Array.Empty<QueuedMessage>());
        }

        public Task<int> CountAsync(Guid driverId, CancellationToken cancellationToken = default)
        {
            if (_messages.TryGetValue(driverId, out var msgs))
                return Task.FromResult(msgs.Count);
            return Task.FromResult(0);
        }
    }

    private sealed class FakeWebSocketPushService : IWebSocketPushService
    {
        public List<(string ConnectionId, string Payload)> PushedMessages { get; } = new();
        public bool FailOnPush { get; set; }

        public Task<bool> PostToConnectionAsync(string connectionId, string payload, CancellationToken cancellationToken = default)
        {
            PushedMessages.Add((connectionId, payload));
            return Task.FromResult(!FailOnPush);
        }
    }
}
