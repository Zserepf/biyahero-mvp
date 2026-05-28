using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.WebSockets;
using Microsoft.Extensions.Logging.Abstractions;

namespace BiyaHero.Api.Tests.WebSockets;

/// <summary>
/// Unit tests for the WebSocket $disconnect handler.
/// Verifies connection cleanup via the byConnectionId GSI lookup and deletion.
/// Requirements: 4.3
/// </summary>
public class DisconnectHandlerTests
{
    private readonly FakeWsConnectionRepository _repository = new();
    private readonly DisconnectHandler _handler;

    public DisconnectHandlerTests()
    {
        _handler = new DisconnectHandler(
            _repository,
            NullLogger<DisconnectHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ExistingConnection_RemovesAndReturnsSuccess()
    {
        // Arrange
        var connectionId = "abc-123-conn";
        _repository.AddConnection(connectionId, Guid.NewGuid());

        // Act
        var result = await _handler.HandleAsync(connectionId);

        // Assert
        Assert.Equal(200, result.StatusCode);
        Assert.Contains(connectionId, _repository.RemovedConnectionIds);
    }

    [Fact]
    public async Task HandleAsync_NonExistentConnection_ReturnsSuccess_IdempotentNoOp()
    {
        // Arrange — no connection registered
        var connectionId = "non-existent-conn";

        // Act
        var result = await _handler.HandleAsync(connectionId);

        // Assert — still returns success (idempotent)
        Assert.Equal(200, result.StatusCode);
        Assert.Contains(connectionId, _repository.RemovedConnectionIds);
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_ReturnsSuccess_NeverFails()
    {
        // Arrange — repository will throw on remove
        var connectionId = "error-conn";
        _repository.ThrowOnRemove = true;

        // Act
        var result = await _handler.HandleAsync(connectionId);

        // Assert — disconnect handler must always succeed
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_EmptyConnectionId_ReturnsSuccess()
    {
        // Arrange — edge case: empty connection ID
        var connectionId = string.Empty;

        // Act
        var result = await _handler.HandleAsync(connectionId);

        // Assert — still returns success
        Assert.Equal(200, result.StatusCode);
    }

    // ─── Fake Repository ──────────────────────────────────────────────────

    private sealed class FakeWsConnectionRepository : IWsConnectionRepository
    {
        private readonly Dictionary<string, WsConnection> _connections = new();

        public List<string> RemovedConnectionIds { get; } = new();
        public bool ThrowOnRemove { get; set; }

        public void AddConnection(string connectionId, Guid userId)
        {
            var conn = new WsConnection(
                id: Guid.NewGuid(),
                createdAt: DateTime.UtcNow,
                updatedAt: DateTime.UtcNow,
                userId: userId,
                role: UserRole.Commuter,
                connectionId: connectionId,
                connectedAt: DateTime.UtcNow,
                subscribedBbox: null,
                expiresAt: DateTime.UtcNow.AddHours(24));
            _connections[connectionId] = conn;
        }

        public Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnRemove)
                throw new InvalidOperationException("Simulated DynamoDB failure");

            RemovedConnectionIds.Add(connectionId);
            _connections.Remove(connectionId);
            return Task.CompletedTask;
        }

        public Task RegisterConnectionAsync(WsConnection connection, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WsConnection>> GetConnectionsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(
                _connections.Values.Where(c => c.UserId == userId).ToList());

        public Task<WsConnection?> GetConnectionByIdAsync(string connectionId, CancellationToken cancellationToken = default)
        {
            _connections.TryGetValue(connectionId, out var conn);
            return Task.FromResult<WsConnection?>(conn);
        }

        public Task UpdateSubscriptionAsync(string connectionId, string? bbox, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WsConnection>> GetSubscribedConnectionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(Array.Empty<WsConnection>());
    }
}
