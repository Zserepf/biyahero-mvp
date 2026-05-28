using System.Text.Json;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Heatmap.Aggregator;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BiyaHero.Api.Tests.Features.Heatmap;

/// <summary>
/// Unit tests for the HeatmapAggregatorHandler (EventBridge-driven Lambda).
/// Validates aggregation correctness, PII exclusion, stale connection handling,
/// and TTL-expired ping exclusion.
/// Requirements: 4.2, 4.3, 4.4, 4.6
/// </summary>
public class HeatmapAggregatorHandlerTests
{
    private readonly FakeWsConnectionRepository _wsConnectionRepository = new();
    private readonly FakeDemandPingRepository _demandPingRepository = new();
    private readonly FakeWebSocketPushService _webSocketPushService = new();
    private readonly HeatmapAggregatorHandler _handler;

    public HeatmapAggregatorHandlerTests()
    {
        _handler = new HeatmapAggregatorHandler(
            _wsConnectionRepository,
            _demandPingRepository,
            _webSocketPushService,
            NullLogger<HeatmapAggregatorHandler>.Instance);
    }

    // ─── No Subscriptions ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoSubscribedConnections_DoesNotPush()
    {
        // Act
        await _handler.HandleAsync();

        // Assert
        Assert.Empty(_webSocketPushService.PushedMessages);
    }

    // ─── Aggregation Correctness ──────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_SingleSubscription_PushesAggregatedTiles()
    {
        // Arrange — one driver subscribed to a bbox with 2 pings in same geohash7
        var connectionId = "driver-conn-1";
        _wsConnectionRepository.AddSubscribedConnection(connectionId, Guid.NewGuid(), "14.5,120.9,14.7,121.1");

        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));
        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));

        // Act
        await _handler.HandleAsync();

        // Assert — one push to the driver with aggregated tiles
        Assert.Single(_webSocketPushService.PushedMessages);
        var (pushedConnId, payload) = _webSocketPushService.PushedMessages[0];
        Assert.Equal(connectionId, pushedConnId);

        // Parse the envelope and verify structure
        var envelope = JsonDocument.Parse(payload).RootElement;
        Assert.Equal("heatmap.delta", envelope.GetProperty("action").GetString());
        Assert.True(envelope.TryGetProperty("requestId", out _));
        Assert.True(envelope.TryGetProperty("emittedAt", out _));

        var tiles = envelope.GetProperty("data").GetProperty("tiles");
        Assert.True(tiles.GetArrayLength() > 0);

        // Verify tile has geohash7, demandCount, vehicleType — no PII
        var firstTile = tiles[0];
        Assert.True(firstTile.TryGetProperty("geohash7", out _));
        Assert.True(firstTile.TryGetProperty("demandCount", out _));
        Assert.True(firstTile.TryGetProperty("vehicleType", out _));
    }

    [Fact]
    public async Task HandleAsync_MultipleDriversSameBbox_PushesToAll()
    {
        // Arrange — two drivers subscribed to the same bbox
        var bbox = "14.5,120.9,14.7,121.1";
        _wsConnectionRepository.AddSubscribedConnection("driver-1", Guid.NewGuid(), bbox);
        _wsConnectionRepository.AddSubscribedConnection("driver-2", Guid.NewGuid(), bbox);

        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));

        // Act
        await _handler.HandleAsync();

        // Assert — both drivers receive the push
        Assert.Equal(2, _webSocketPushService.PushedMessages.Count);
        Assert.Contains(_webSocketPushService.PushedMessages, m => m.ConnectionId == "driver-1");
        Assert.Contains(_webSocketPushService.PushedMessages, m => m.ConnectionId == "driver-2");
    }

    [Fact]
    public async Task HandleAsync_DifferentVehicleTypes_AggregatesSeparately()
    {
        // Arrange
        var connectionId = "driver-vtype";
        _wsConnectionRepository.AddSubscribedConnection(connectionId, Guid.NewGuid(), "14.5,120.9,14.7,121.1");

        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));
        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Bus));
        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));

        // Act
        await _handler.HandleAsync();

        // Assert
        Assert.Single(_webSocketPushService.PushedMessages);
        var envelope = JsonDocument.Parse(_webSocketPushService.PushedMessages[0].Payload).RootElement;
        var tiles = envelope.GetProperty("data").GetProperty("tiles");

        // Should have 2 tiles: one for Jeepney (count 2), one for Bus (count 1)
        Assert.Equal(2, tiles.GetArrayLength());
    }

    // ─── PII Exclusion (Req 4.6) ─────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_PushedTiles_ContainNoPII()
    {
        // Arrange
        var connectionId = "driver-pii";
        _wsConnectionRepository.AddSubscribedConnection(connectionId, Guid.NewGuid(), "14.5,120.9,14.7,121.1");

        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));

        // Act
        await _handler.HandleAsync();

        // Assert — verify no PII in the pushed payload
        var payload = _webSocketPushService.PushedMessages[0].Payload;
        var envelope = JsonDocument.Parse(payload).RootElement;
        var tiles = envelope.GetProperty("data").GetProperty("tiles");

        foreach (var tile in tiles.EnumerateArray())
        {
            // Must NOT contain commuter-identifying fields
            Assert.False(tile.TryGetProperty("commuterId", out _));
            Assert.False(tile.TryGetProperty("name", out _));
            Assert.False(tile.TryGetProperty("email", out _));
            Assert.False(tile.TryGetProperty("deviceId", out _));
            Assert.False(tile.TryGetProperty("userId", out _));
            Assert.False(tile.TryGetProperty("latitude", out _));
            Assert.False(tile.TryGetProperty("longitude", out _));
        }
    }

    // ─── Stale Connection Handling ────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_StaleConnection_RemovesIt()
    {
        // Arrange — push will fail (simulating 410 Gone)
        var connectionId = "stale-conn";
        _wsConnectionRepository.AddSubscribedConnection(connectionId, Guid.NewGuid(), "14.5,120.9,14.7,121.1");
        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));
        _webSocketPushService.FailForConnections.Add(connectionId);

        // Act
        await _handler.HandleAsync();

        // Assert — stale connection was removed
        Assert.Contains(connectionId, _wsConnectionRepository.RemovedConnectionIds);
    }

    [Fact]
    public async Task HandleAsync_MixedStaleAndActive_OnlyRemovesStale()
    {
        // Arrange
        var bbox = "14.5,120.9,14.7,121.1";
        _wsConnectionRepository.AddSubscribedConnection("active-conn", Guid.NewGuid(), bbox);
        _wsConnectionRepository.AddSubscribedConnection("stale-conn", Guid.NewGuid(), bbox);
        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));
        _webSocketPushService.FailForConnections.Add("stale-conn");

        // Act
        await _handler.HandleAsync();

        // Assert
        Assert.Contains("stale-conn", _wsConnectionRepository.RemovedConnectionIds);
        Assert.DoesNotContain("active-conn", _wsConnectionRepository.RemovedConnectionIds);
    }

    // ─── TTL-Expired Pings Excluded (Req 4.4) ────────────────────────────

    [Fact]
    public async Task HandleAsync_ExpiredPings_ExcludedFromAggregation()
    {
        // Arrange
        var connectionId = "driver-ttl";
        _wsConnectionRepository.AddSubscribedConnection(connectionId, Guid.NewGuid(), "14.5,120.9,14.7,121.1");

        // Add one active ping and one expired ping
        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));
        _demandPingRepository.AddExpiredPing(CreateExpiredPing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));

        // Act
        await _handler.HandleAsync();

        // Assert — only the active ping should be counted
        Assert.Single(_webSocketPushService.PushedMessages);
        var envelope = JsonDocument.Parse(_webSocketPushService.PushedMessages[0].Payload).RootElement;
        var tiles = envelope.GetProperty("data").GetProperty("tiles");

        if (tiles.GetArrayLength() > 0)
        {
            var tile = tiles[0];
            Assert.Equal(1, tile.GetProperty("demandCount").GetInt32());
        }
    }

    // ─── Pings Outside Bbox Excluded ──────────────────────────────────────

    [Fact]
    public async Task HandleAsync_PingsOutsideBbox_ExcludedFromAggregation()
    {
        // Arrange — bbox is small area around Manila
        var connectionId = "driver-bbox-filter";
        _wsConnectionRepository.AddSubscribedConnection(connectionId, Guid.NewGuid(), "14.5,120.9,14.6,121.0");

        // Ping inside bbox
        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));
        // Ping outside bbox (different lat/lng)
        _demandPingRepository.AddPing(CreatePing(15.5, 122.0, "wdw6a", "wdw6abc", VehicleType.Jeepney));

        // Act
        await _handler.HandleAsync();

        // Assert — only the ping inside the bbox should appear
        Assert.Single(_webSocketPushService.PushedMessages);
        var envelope = JsonDocument.Parse(_webSocketPushService.PushedMessages[0].Payload).RootElement;
        var tiles = envelope.GetProperty("data").GetProperty("tiles");

        // Only tiles within the bbox should be present
        foreach (var tile in tiles.EnumerateArray())
        {
            Assert.Equal("wdw5nyq", tile.GetProperty("geohash7").GetString());
        }
    }

    // ─── Invalid Bbox Format ──────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_InvalidBboxFormat_SkipsConnection()
    {
        // Arrange — invalid bbox format
        _wsConnectionRepository.AddSubscribedConnection("conn-bad-bbox", Guid.NewGuid(), "invalid-bbox");
        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));

        // Act
        await _handler.HandleAsync();

        // Assert — no push sent (invalid bbox skipped)
        Assert.Empty(_webSocketPushService.PushedMessages);
    }

    // ─── Envelope Structure ───────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_EnvelopeHasCorrectStructure()
    {
        // Arrange
        _wsConnectionRepository.AddSubscribedConnection("driver-env", Guid.NewGuid(), "14.5,120.9,14.7,121.1");
        _demandPingRepository.AddPing(CreatePing(14.55, 120.95, "wdw5n", "wdw5nyq", VehicleType.Jeepney));

        // Act
        await _handler.HandleAsync();

        // Assert — envelope matches WebSocket protocol spec
        var payload = _webSocketPushService.PushedMessages[0].Payload;
        var envelope = JsonDocument.Parse(payload).RootElement;

        Assert.Equal("heatmap.delta", envelope.GetProperty("action").GetString());
        Assert.True(Guid.TryParse(envelope.GetProperty("requestId").GetString(), out _));
        Assert.True(DateTime.TryParse(envelope.GetProperty("emittedAt").GetString(), out _));
        Assert.True(envelope.TryGetProperty("data", out var data));
        Assert.True(data.TryGetProperty("tiles", out var tiles));
        Assert.Equal(JsonValueKind.Array, tiles.ValueKind);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static DemandPing CreatePing(double lat, double lng, string geohash5, string geohash7, VehicleType vehicleType)
    {
        return new DemandPing(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            commuterId: Guid.NewGuid(),
            latitude: lat,
            longitude: lng,
            geohash5: geohash5,
            geohash7: geohash7,
            vehicleType: vehicleType,
            expiresAt: DateTime.UtcNow.AddMinutes(5));
    }

    private static DemandPing CreateExpiredPing(double lat, double lng, string geohash5, string geohash7, VehicleType vehicleType)
    {
        return new DemandPing(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow.AddMinutes(-10),
            updatedAt: DateTime.UtcNow.AddMinutes(-10),
            commuterId: Guid.NewGuid(),
            latitude: lat,
            longitude: lng,
            geohash5: geohash5,
            geohash7: geohash7,
            vehicleType: vehicleType,
            expiresAt: DateTime.UtcNow.AddMinutes(-5)); // Already expired
    }

    // ─── Fake Repositories ──────────────────────────────────────────────────

    private sealed class FakeWsConnectionRepository : IWsConnectionRepository
    {
        private readonly List<WsConnection> _subscribedConnections = new();
        public List<string> RemovedConnectionIds { get; } = new();

        public void AddSubscribedConnection(string connectionId, Guid userId, string bbox)
        {
            _subscribedConnections.Add(new WsConnection(
                id: Guid.NewGuid(),
                createdAt: DateTime.UtcNow,
                updatedAt: DateTime.UtcNow,
                userId: userId,
                role: UserRole.Driver,
                connectionId: connectionId,
                connectedAt: DateTime.UtcNow,
                subscribedBbox: bbox,
                expiresAt: DateTime.UtcNow.AddHours(24)));
        }

        public Task<IReadOnlyList<WsConnection>> GetSubscribedConnectionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(_subscribedConnections.ToList());

        public Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
        {
            RemovedConnectionIds.Add(connectionId);
            return Task.CompletedTask;
        }

        public Task RegisterConnectionAsync(WsConnection connection, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WsConnection>> GetConnectionsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(Array.Empty<WsConnection>());

        public Task<WsConnection?> GetConnectionByIdAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.FromResult<WsConnection?>(null);

        public Task UpdateSubscriptionAsync(string connectionId, string? bbox, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeDemandPingRepository : IDemandPingRepository
    {
        private readonly List<DemandPing> _activePings = new();
        private readonly List<DemandPing> _expiredPings = new();

        public void AddPing(DemandPing ping) => _activePings.Add(ping);
        public void AddExpiredPing(DemandPing ping) => _expiredPings.Add(ping);

        public Task<IReadOnlyList<DemandPing>> QueryByBboxAsync(
            IEnumerable<string> geohash5Cells,
            CancellationToken cancellationToken = default)
        {
            var cells = geohash5Cells.ToHashSet();
            var now = DateTime.UtcNow;

            // Only return active (non-expired) pings matching the geohash5 cells
            var result = _activePings
                .Where(p => cells.Contains(p.Geohash5) && p.ExpiresAt > now)
                .ToList();

            // Expired pings should NOT be returned (simulating DynamoDB TTL filtering)
            // _expiredPings are intentionally excluded

            return Task.FromResult<IReadOnlyList<DemandPing>>(result);
        }

        public Task<bool> PutPingAsync(DemandPing ping, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<DemandPing>> GetActivePingsByGeohash5Async(string geohash5, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DemandPing>>(Array.Empty<DemandPing>());

        public Task<DemandPing?> GetActivePingByCommuterAsync(Guid commuterId, CancellationToken cancellationToken = default)
            => Task.FromResult<DemandPing?>(null);

        public Task DeletePingAsync(Guid commuterId, Guid pingId, string geohash5, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

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

    private sealed class FakeWebSocketPushService : IWebSocketPushService
    {
        public List<(string ConnectionId, string Payload)> PushedMessages { get; } = new();
        public HashSet<string> FailForConnections { get; } = new();

        public Task<bool> PostToConnectionAsync(string connectionId, string payload, CancellationToken cancellationToken = default)
        {
            PushedMessages.Add((connectionId, payload));
            return Task.FromResult(!FailForConnections.Contains(connectionId));
        }
    }
}
