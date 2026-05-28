using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.WebSockets;
using Microsoft.Extensions.Logging.Abstractions;

namespace BiyaHero.Api.Tests.WebSockets;

/// <summary>
/// Unit tests for the WebSocket cancel-demand handler.
/// Verifies auth check, active ping lookup, deletion, and no-op behavior.
/// Requirements: 4.5
/// </summary>
public class CancelDemandHandlerTests
{
    private readonly FakeWsConnectionRepository _wsConnectionRepository = new();
    private readonly FakeDemandPingRepository _demandPingRepository = new();
    private readonly CancelDemandHandler _handler;

    public CancelDemandHandlerTests()
    {
        _handler = new CancelDemandHandler(
            _wsConnectionRepository,
            _demandPingRepository,
            NullLogger<CancelDemandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_UnauthenticatedConnection_ReturnsAuthFailureWith4001()
    {
        // Arrange — no connection registered for this connectionId
        var connectionId = "unknown-conn-id";
        var requestId = Guid.NewGuid().ToString();

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId);

        // Assert
        Assert.True(result.IsAuthFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(4001, result.CloseCode);
        Assert.NotNull(result.CloseReason);
    }

    [Fact]
    public async Task HandleAsync_AuthenticatedWithActivePing_DeletesPingAndReturnsSuccess()
    {
        // Arrange
        var connectionId = "conn-abc";
        var commuterId = Guid.NewGuid();
        var pingId = Guid.NewGuid();
        var geohash5 = "wdw2q";

        _wsConnectionRepository.AddConnection(connectionId, commuterId);
        _demandPingRepository.AddActivePing(commuterId, pingId, geohash5);

        var requestId = Guid.NewGuid().ToString();

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsNoOp);
        Assert.False(result.IsAuthFailure);
        Assert.Equal(requestId, result.RequestId);
        Assert.Contains(pingId, _demandPingRepository.DeletedPingIds);
    }

    [Fact]
    public async Task HandleAsync_AuthenticatedWithNoActivePing_ReturnsNoOp()
    {
        // Arrange — connection exists but no active ping
        var connectionId = "conn-xyz";
        var commuterId = Guid.NewGuid();

        _wsConnectionRepository.AddConnection(connectionId, commuterId);

        var requestId = Guid.NewGuid().ToString();

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.IsNoOp);
        Assert.False(result.IsAuthFailure);
        Assert.Equal(requestId, result.RequestId);
        Assert.Empty(_demandPingRepository.DeletedPingIds);
    }

    [Fact]
    public async Task HandleAsync_DeletesCorrectPingByCommuterIdAndGeohash()
    {
        // Arrange — two different commuters with pings
        var connectionId = "conn-target";
        var targetCommuterId = Guid.NewGuid();
        var otherCommuterId = Guid.NewGuid();
        var targetPingId = Guid.NewGuid();
        var otherPingId = Guid.NewGuid();

        _wsConnectionRepository.AddConnection(connectionId, targetCommuterId);
        _demandPingRepository.AddActivePing(targetCommuterId, targetPingId, "wdw2q");
        _demandPingRepository.AddActivePing(otherCommuterId, otherPingId, "wdw3r");

        var requestId = Guid.NewGuid().ToString();

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId);

        // Assert — only the target commuter's ping was deleted
        Assert.True(result.IsSuccess);
        Assert.Contains(targetPingId, _demandPingRepository.DeletedPingIds);
        Assert.DoesNotContain(otherPingId, _demandPingRepository.DeletedPingIds);
    }

    // ─── Fake Repositories ──────────────────────────────────────────────────

    private sealed class FakeWsConnectionRepository : IWsConnectionRepository
    {
        private readonly Dictionary<string, WsConnection> _connections = new();

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

        public Task<WsConnection?> GetConnectionByIdAsync(string connectionId, CancellationToken cancellationToken = default)
        {
            _connections.TryGetValue(connectionId, out var conn);
            return Task.FromResult<WsConnection?>(conn);
        }

        public Task RegisterConnectionAsync(WsConnection connection, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WsConnection>> GetConnectionsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(
                _connections.Values.Where(c => c.UserId == userId).ToList());

        public Task UpdateSubscriptionAsync(string connectionId, string? bbox, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WsConnection>> GetSubscribedConnectionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(Array.Empty<WsConnection>());
    }

    private sealed class FakeDemandPingRepository : IDemandPingRepository
    {
        private readonly Dictionary<Guid, DemandPing> _pingsByCommuter = new();

        public List<Guid> DeletedPingIds { get; } = new();

        public void AddActivePing(Guid commuterId, Guid pingId, string geohash5)
        {
            var ping = new DemandPing(
                id: pingId,
                createdAt: DateTime.UtcNow,
                updatedAt: DateTime.UtcNow,
                commuterId: commuterId,
                latitude: 14.5995,
                longitude: 120.9842,
                geohash5: geohash5,
                geohash7: geohash5 + "ab",
                vehicleType: VehicleType.Jeepney,
                expiresAt: DateTime.UtcNow.AddMinutes(5));
            _pingsByCommuter[commuterId] = ping;
        }

        public Task<DemandPing?> GetActivePingByCommuterAsync(Guid commuterId, CancellationToken cancellationToken = default)
        {
            _pingsByCommuter.TryGetValue(commuterId, out var ping);
            return Task.FromResult<DemandPing?>(ping);
        }

        public Task DeletePingAsync(Guid commuterId, Guid pingId, string geohash5, CancellationToken cancellationToken = default)
        {
            DeletedPingIds.Add(pingId);
            _pingsByCommuter.Remove(commuterId);
            return Task.CompletedTask;
        }

        public Task<bool> PutPingAsync(DemandPing ping, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<DemandPing>> GetActivePingsByGeohash5Async(string geohash5, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DemandPing>>(Array.Empty<DemandPing>());

        public Task<IReadOnlyList<DemandPing>> QueryByBboxAsync(IEnumerable<string> geohash5Cells, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DemandPing>>(Array.Empty<DemandPing>());

        // IDynamoRepository<DemandPing> base methods
        public Task<DemandPing?> GetItemAsync(string pk, string sk, CancellationToken cancellationToken = default)
            => Task.FromResult<DemandPing?>(null);

        public Task<bool> PutItemAsync(DemandPing entity, bool conditionalOnNotExists = false, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<DemandPing>> QueryAsync(string pk, string? skPrefix = null, bool scanForward = true, int? limit = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DemandPing>>(Array.Empty<DemandPing>());

        public Task<IReadOnlyList<DemandPing>> QueryByIndexAsync(string indexName, string indexPk, string? indexSkPrefix = null, bool scanForward = true, int? limit = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DemandPing>>(Array.Empty<DemandPing>());

        public Task DeleteAsync(string pk, string sk, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
