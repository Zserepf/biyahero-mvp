using System.Text.Json;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.WebSockets;
using Microsoft.Extensions.Logging.Abstractions;

namespace BiyaHero.Api.Tests.WebSockets;

/// <summary>
/// Unit tests for the WebSocket subscribe-heatmap handler.
/// Verifies bbox parsing, coordinate validation, subscription storage, and anonymous access.
/// Requirements: 4.3, 4.7
/// </summary>
public class SubscribeHeatmapHandlerTests
{
    private readonly FakeWsConnectionRepository _repository = new();
    private readonly SubscribeHeatmapHandler _handler;

    public SubscribeHeatmapHandlerTests()
    {
        _handler = new SubscribeHeatmapHandler(
            _repository,
            NullLogger<SubscribeHeatmapHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ValidBbox_StoresSubscriptionAndReturnsSuccess()
    {
        // Arrange
        var connectionId = "conn-123";
        var requestId = "req-abc";
        var data = JsonDocument.Parse("""{"minLat":14.5,"minLng":120.9,"maxLat":14.7,"maxLng":121.1}""").RootElement;

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("14.5,120.9,14.7,121.1", result.Bbox);
        Assert.Equal(connectionId, _repository.LastUpdatedConnectionId);
        Assert.Equal("14.5,120.9,14.7,121.1", _repository.LastUpdatedBbox);
    }

    [Fact]
    public async Task HandleAsync_AnonymousConnection_AllowsSubscription()
    {
        // Arrange — no auth check needed; anonymous subscribe is allowed (Req 4.7)
        var connectionId = "anon-conn-456";
        var requestId = "req-def";
        var data = JsonDocument.Parse("""{"minLat":10.0,"minLng":118.0,"maxLat":12.0,"maxLng":120.0}""").RootElement;

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("10,118,12,120", result.Bbox);
        Assert.Equal(connectionId, _repository.LastUpdatedConnectionId);
    }

    [Fact]
    public async Task HandleAsync_NullData_ReturnsValidationError()
    {
        // Arrange
        var connectionId = "conn-789";
        var requestId = "req-ghi";

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId, null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Missing bbox data.", result.ErrorMessage);
        Assert.Null(_repository.LastUpdatedConnectionId);
    }

    [Fact]
    public async Task HandleAsync_MissingMinLat_ReturnsValidationError()
    {
        // Arrange
        var connectionId = "conn-001";
        var requestId = "req-001";
        var data = JsonDocument.Parse("""{"minLng":120.9,"maxLat":14.7,"maxLng":121.1}""").RootElement;

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId, data);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("minLat", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_MissingMaxLng_ReturnsValidationError()
    {
        // Arrange
        var connectionId = "conn-002";
        var requestId = "req-002";
        var data = JsonDocument.Parse("""{"minLat":14.5,"minLng":120.9,"maxLat":14.7}""").RootElement;

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId, data);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("maxLng", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_LatitudeOutOfRange_ReturnsValidationError()
    {
        // Arrange — minLat = 95 is out of valid range [-90, 90]
        var connectionId = "conn-003";
        var requestId = "req-003";
        var data = JsonDocument.Parse("""{"minLat":95.0,"minLng":120.9,"maxLat":14.7,"maxLng":121.1}""").RootElement;

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId, data);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("minLat", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_LongitudeOutOfRange_ReturnsValidationError()
    {
        // Arrange — maxLng = 200 is out of valid range [-180, 180]
        var connectionId = "conn-004";
        var requestId = "req-004";
        var data = JsonDocument.Parse("""{"minLat":14.5,"minLng":120.9,"maxLat":14.7,"maxLng":200.0}""").RootElement;

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId, data);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("maxLng", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_MinLatGreaterThanMaxLat_ReturnsValidationError()
    {
        // Arrange — minLat > maxLat is invalid
        var connectionId = "conn-005";
        var requestId = "req-005";
        var data = JsonDocument.Parse("""{"minLat":15.0,"minLng":120.9,"maxLat":14.0,"maxLng":121.1}""").RootElement;

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId, data);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("minLat must be less than or equal to maxLat", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_MinLngGreaterThanMaxLng_ReturnsValidationError()
    {
        // Arrange — minLng > maxLng is invalid
        var connectionId = "conn-006";
        var requestId = "req-006";
        var data = JsonDocument.Parse("""{"minLat":14.5,"minLng":122.0,"maxLat":14.7,"maxLng":121.0}""").RootElement;

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId, data);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("minLng must be less than or equal to maxLng", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_NonObjectData_ReturnsValidationError()
    {
        // Arrange — data is a string, not an object
        var connectionId = "conn-007";
        var requestId = "req-007";
        var data = JsonDocument.Parse("""  "not an object"  """).RootElement;

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId, data);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("JSON object", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_NonNumericCoordinate_ReturnsValidationError()
    {
        // Arrange — minLat is a string, not a number
        var connectionId = "conn-008";
        var requestId = "req-008";
        var data = JsonDocument.Parse("""{"minLat":"abc","minLng":120.9,"maxLat":14.7,"maxLng":121.1}""").RootElement;

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId, data);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("minLat", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_BoundaryValues_AcceptsValidExtremes()
    {
        // Arrange — valid extreme coordinates
        var connectionId = "conn-009";
        var requestId = "req-009";
        var data = JsonDocument.Parse("""{"minLat":-90.0,"minLng":-180.0,"maxLat":90.0,"maxLng":180.0}""").RootElement;

        // Act
        var result = await _handler.HandleAsync(connectionId, requestId, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("-90,-180,90,180", result.Bbox);
    }

    // ─── Fake Repository ──────────────────────────────────────────────────

    private sealed class FakeWsConnectionRepository : IWsConnectionRepository
    {
        public string? LastUpdatedConnectionId { get; private set; }
        public string? LastUpdatedBbox { get; private set; }

        public Task UpdateSubscriptionAsync(string connectionId, string? bbox, CancellationToken cancellationToken = default)
        {
            LastUpdatedConnectionId = connectionId;
            LastUpdatedBbox = bbox;
            return Task.CompletedTask;
        }

        public Task RegisterConnectionAsync(WsConnection connection, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WsConnection>> GetConnectionsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(Array.Empty<WsConnection>());

        public Task<WsConnection?> GetConnectionByIdAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.FromResult<WsConnection?>(null);

        public Task<IReadOnlyList<WsConnection>> GetSubscribedConnectionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(Array.Empty<WsConnection>());
    }
}
