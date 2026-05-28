using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.WebSockets;
using Microsoft.Extensions.Logging.Abstractions;

namespace BiyaHero.Api.Tests.WebSockets;

/// <summary>
/// Unit tests for the WebSocket demand-ping handler.
/// Verifies authentication enforcement, Philippines bbox validation,
/// vehicle type validation, geohash encoding, and DemandPing persistence.
/// Requirements: 4.1, 4.7, 4.8, 4.9
/// </summary>
public class DemandPingHandlerTests
{
    private readonly FakeWsConnectionRepository _wsConnectionRepository = new();
    private readonly FakeDemandPingRepository _demandPingRepository = new();
    private readonly DemandPingHandler _handler;

    public DemandPingHandlerTests()
    {
        _handler = new DemandPingHandler(
            _wsConnectionRepository,
            _demandPingRepository,
            TimeProvider.System,
            NullLogger<DemandPingHandler>.Instance);
    }

    // ─── Auth Tests (Req 4.7) ─────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_UnauthenticatedConnection_ReturnsAuthFailureWith4001()
    {
        // Arrange — no connection registered for this connectionId
        var request = new DemandPingRequest
        {
            Latitude = 14.5995,
            Longitude = 120.9842,
            VehicleType = VehicleType.Jeepney
        };

        // Act
        var result = await _handler.HandleAsync("unknown-conn", request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsAuthFailure);
        Assert.Equal(4001, result.CloseCode);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_AuthenticatedConnection_DoesNotReturnAuthFailure()
    {
        // Arrange
        var connectionId = "conn-auth";
        var commuterId = Guid.NewGuid();
        _wsConnectionRepository.AddConnection(connectionId, commuterId);

        var request = new DemandPingRequest
        {
            Latitude = 14.5995,
            Longitude = 120.9842,
            VehicleType = VehicleType.Jeepney
        };

        // Act
        var result = await _handler.HandleAsync(connectionId, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsAuthFailure);
    }

    // ─── Philippines Bbox Validation (Req 4.8, 4.9) ───────────────────────

    [Theory]
    [InlineData(14.5995, 120.9842)]  // Manila
    [InlineData(4.5, 116.0)]          // Southwest corner of PH bbox
    [InlineData(21.5, 127.0)]         // Northeast corner of PH bbox
    [InlineData(10.3157, 123.8854)]   // Cebu
    [InlineData(7.0731, 125.6128)]    // Davao
    public async Task HandleAsync_ValidPhilippinesCoordinates_ReturnsSuccess(double lat, double lng)
    {
        // Arrange
        var connectionId = "conn-ph";
        _wsConnectionRepository.AddConnection(connectionId, Guid.NewGuid());

        var request = new DemandPingRequest
        {
            Latitude = lat,
            Longitude = lng,
            VehicleType = VehicleType.Jeepney
        };

        // Act
        var result = await _handler.HandleAsync(connectionId, request);

        // Assert — PH-wide pings accepted (Req 4.9)
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(4.4, 120.0)]    // Below PH south boundary
    [InlineData(21.6, 120.0)]   // Above PH north boundary
    [InlineData(14.5, 115.9)]   // West of PH boundary
    [InlineData(14.5, 127.1)]   // East of PH boundary
    [InlineData(0.0, 0.0)]      // Completely outside PH
    [InlineData(-33.8, 151.2)]  // Sydney, Australia
    public async Task HandleAsync_CoordinatesOutsidePhilippines_ReturnsValidationError(double lat, double lng)
    {
        // Arrange
        var connectionId = "conn-outside";
        _wsConnectionRepository.AddConnection(connectionId, Guid.NewGuid());

        var request = new DemandPingRequest
        {
            Latitude = lat,
            Longitude = lng,
            VehicleType = VehicleType.Jeepney
        };

        // Act
        var result = await _handler.HandleAsync(connectionId, request);

        // Assert — rejected without persistence (Req 4.8)
        Assert.False(result.IsSuccess);
        Assert.False(result.IsAuthFailure);
        Assert.Contains("coordinates", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_demandPingRepository.PersistedPings);
    }

    // ─── Vehicle Type Validation (Req 4.8) ────────────────────────────────

    [Fact]
    public async Task HandleAsync_UnsupportedVehicleType_ReturnsValidationError()
    {
        // Arrange
        var connectionId = "conn-vtype";
        _wsConnectionRepository.AddConnection(connectionId, Guid.NewGuid());

        var request = new DemandPingRequest
        {
            Latitude = 14.5995,
            Longitude = 120.9842,
            VehicleType = (VehicleType)999 // Invalid enum value
        };

        // Act
        var result = await _handler.HandleAsync(connectionId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(result.IsAuthFailure);
        Assert.Contains("vehicle type", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_demandPingRepository.PersistedPings);
    }

    // ─── Persistence Tests (Req 4.1) ─────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidRequest_PersistsPingWithCorrectFields()
    {
        // Arrange
        var connectionId = "conn-persist";
        var commuterId = Guid.NewGuid();
        _wsConnectionRepository.AddConnection(connectionId, commuterId);

        var request = new DemandPingRequest
        {
            Latitude = 14.5995,
            Longitude = 120.9842,
            VehicleType = VehicleType.Jeepney
        };

        // Act
        var result = await _handler.HandleAsync(connectionId, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(_demandPingRepository.PersistedPings);

        var persisted = _demandPingRepository.PersistedPings[0];
        Assert.Equal(commuterId, persisted.CommuterId);
        Assert.Equal(14.5995, persisted.Latitude);
        Assert.Equal(120.9842, persisted.Longitude);
        Assert.Equal(VehicleType.Jeepney, persisted.VehicleType);
        Assert.NotEmpty(persisted.Geohash5);
        Assert.NotEmpty(persisted.Geohash7);
        Assert.Equal(5, persisted.Geohash5.Length);
        Assert.Equal(7, persisted.Geohash7.Length);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_SetsTtlTo5Minutes()
    {
        // Arrange
        var connectionId = "conn-ttl";
        _wsConnectionRepository.AddConnection(connectionId, Guid.NewGuid());

        var request = new DemandPingRequest
        {
            Latitude = 14.5995,
            Longitude = 120.9842,
            VehicleType = VehicleType.Bus
        };

        // Act
        var result = await _handler.HandleAsync(connectionId, request);

        // Assert — TTL should be approximately 5 minutes from now (Req 4.1)
        Assert.True(result.IsSuccess);
        var persisted = _demandPingRepository.PersistedPings[0];
        var expectedExpiry = DateTime.UtcNow.AddMinutes(5);
        Assert.True(persisted.ExpiresAt > DateTime.UtcNow.AddMinutes(4));
        Assert.True(persisted.ExpiresAt < DateTime.UtcNow.AddMinutes(6));
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_ReturnsPingIdAndGeohash7()
    {
        // Arrange
        var connectionId = "conn-result";
        _wsConnectionRepository.AddConnection(connectionId, Guid.NewGuid());

        var request = new DemandPingRequest
        {
            Latitude = 14.5995,
            Longitude = 120.9842,
            VehicleType = VehicleType.Jeepney
        };

        // Act
        var result = await _handler.HandleAsync(connectionId, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PingId);
        Assert.NotEqual(Guid.Empty, result.PingId.Value);
        Assert.NotNull(result.Geohash7);
        Assert.Equal(7, result.Geohash7.Length);
        Assert.NotNull(result.ExpiresAt);
    }

    [Fact]
    public async Task HandleAsync_InvalidCoordinates_DoesNotPersist()
    {
        // Arrange
        var connectionId = "conn-no-persist";
        _wsConnectionRepository.AddConnection(connectionId, Guid.NewGuid());

        var request = new DemandPingRequest
        {
            Latitude = 0.0, // Outside Philippines
            Longitude = 0.0,
            VehicleType = VehicleType.Jeepney
        };

        // Act
        var result = await _handler.HandleAsync(connectionId, request);

        // Assert — rejected pings are NOT persisted (Req 4.8)
        Assert.False(result.IsSuccess);
        Assert.Empty(_demandPingRepository.PersistedPings);
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
            => Task.FromResult<IReadOnlyList<WsConnection>>(Array.Empty<WsConnection>());

        public Task UpdateSubscriptionAsync(string connectionId, string? bbox, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WsConnection>> GetSubscribedConnectionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(Array.Empty<WsConnection>());
    }

    private sealed class FakeDemandPingRepository : IDemandPingRepository
    {
        public List<DemandPing> PersistedPings { get; } = new();

        public Task<bool> PutPingAsync(DemandPing ping, CancellationToken cancellationToken = default)
        {
            PersistedPings.Add(ping);
            return Task.FromResult(true);
        }

        public Task<DemandPing?> GetActivePingByCommuterAsync(Guid commuterId, CancellationToken cancellationToken = default)
            => Task.FromResult<DemandPing?>(null);

        public Task DeletePingAsync(Guid commuterId, Guid pingId, string geohash5, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<DemandPing>> GetActivePingsByGeohash5Async(string geohash5, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DemandPing>>(Array.Empty<DemandPing>());

        public Task<IReadOnlyList<DemandPing>> QueryByBboxAsync(IEnumerable<string> geohash5Cells, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DemandPing>>(Array.Empty<DemandPing>());

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
