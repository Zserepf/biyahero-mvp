using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Heatmap.GetTiles;
using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Tests.Features.Heatmap;

/// <summary>
/// Unit tests for the GetTilesHandler.
/// Validates bbox validation, anonymous access, geohash7 aggregation,
/// vehicle type filtering, and PII exclusion.
/// 
/// Validates: Requirements 4.2, 4.6
/// </summary>
public class GetTilesHandlerTests
{
    private readonly FakeDemandPingRepository _repository;
    private readonly GetTilesHandler _handler;

    public GetTilesHandlerTests()
    {
        _repository = new FakeDemandPingRepository();
        _handler = new GetTilesHandler(_repository);
    }

    // ─── Validation Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_MinLatOutOfRange_ReturnsValidationError()
    {
        var result = await _handler.HandleAsync(-91, 120, 15, 121, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Latitude", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_MaxLatOutOfRange_ReturnsValidationError()
    {
        var result = await _handler.HandleAsync(14, 120, 91, 121, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Latitude", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_MinLngOutOfRange_ReturnsValidationError()
    {
        var result = await _handler.HandleAsync(14, -181, 15, 121, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Longitude", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_MaxLngOutOfRange_ReturnsValidationError()
    {
        var result = await _handler.HandleAsync(14, 120, 15, 181, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Longitude", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_MinLatGreaterThanMaxLat_ReturnsValidationError()
    {
        var result = await _handler.HandleAsync(15, 120, 14, 121, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("minLat", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_MinLngGreaterThanMaxLng_ReturnsValidationError()
    {
        var result = await _handler.HandleAsync(14, 121, 15, 120, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("minLng", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_UnsupportedVehicleType_ReturnsValidationError()
    {
        var result = await _handler.HandleAsync(14, 120, 15, 121, "helicopter");

        Assert.False(result.IsSuccess);
        Assert.Contains("Unsupported vehicle type", result.ErrorMessage);
    }

    // ─── Success / Aggregation Tests ──────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidBbox_NoPings_ReturnsEmptyTiles()
    {
        var result = await _handler.HandleAsync(14.5, 120.9, 14.6, 121.0, null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Tiles);
    }

    [Fact]
    public async Task HandleAsync_WithActivePings_AggregatesByGeohash7()
    {
        // Add pings in the same geohash7 cell
        var geohash7 = "wdw5nyq";
        _repository.AddPing(CreatePing(14.55, 120.95, geohash7, VehicleType.Jeepney));
        _repository.AddPing(CreatePing(14.55, 120.95, geohash7, VehicleType.Jeepney));

        var result = await _handler.HandleAsync(14.5, 120.9, 14.6, 121.0, null);

        Assert.True(result.IsSuccess);
        var tile = Assert.Single(result.Tiles);
        Assert.Equal(geohash7, tile.Geohash7);
        Assert.Equal(2, tile.DemandCount);
        Assert.Equal("Jeepney", tile.VehicleType);
    }

    [Fact]
    public async Task HandleAsync_DifferentVehicleTypes_SeparateTiles()
    {
        var geohash7 = "wdw5nyq";
        _repository.AddPing(CreatePing(14.55, 120.95, geohash7, VehicleType.Jeepney));
        _repository.AddPing(CreatePing(14.55, 120.95, geohash7, VehicleType.Bus));

        var result = await _handler.HandleAsync(14.5, 120.9, 14.6, 121.0, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Tiles.Count);
        Assert.Contains(result.Tiles, t => t.VehicleType == "Jeepney" && t.DemandCount == 1);
        Assert.Contains(result.Tiles, t => t.VehicleType == "Bus" && t.DemandCount == 1);
    }

    [Fact]
    public async Task HandleAsync_VehicleTypeFilter_ReturnsOnlyMatchingType()
    {
        var geohash7 = "wdw5nyq";
        _repository.AddPing(CreatePing(14.55, 120.95, geohash7, VehicleType.Jeepney));
        _repository.AddPing(CreatePing(14.55, 120.95, geohash7, VehicleType.Bus));

        var result = await _handler.HandleAsync(14.5, 120.9, 14.6, 121.0, "jeepney");

        Assert.True(result.IsSuccess);
        var tile = Assert.Single(result.Tiles);
        Assert.Equal("Jeepney", tile.VehicleType);
        Assert.Equal(1, tile.DemandCount);
    }

    [Fact]
    public async Task HandleAsync_PingsOutsideBbox_AreExcluded()
    {
        var geohash7Inside = "wdw5nyq";
        var geohash7Outside = "wdw6abc";
        _repository.AddPing(CreatePing(14.55, 120.95, geohash7Inside, VehicleType.Jeepney));
        // This ping is outside the bbox lat/lng range
        _repository.AddPing(CreatePing(15.5, 122.0, geohash7Outside, VehicleType.Jeepney));

        var result = await _handler.HandleAsync(14.5, 120.9, 14.6, 121.0, null);

        Assert.True(result.IsSuccess);
        // Only the ping inside the bbox should be included
        Assert.Single(result.Tiles);
        Assert.Equal(geohash7Inside, result.Tiles[0].Geohash7);
    }

    // ─── PII Exclusion Tests (Req 4.6) ───────────────────────────────────

    [Fact]
    public async Task HandleAsync_ResponseContainsNoPII()
    {
        var geohash7 = "wdw5nyq";
        _repository.AddPing(CreatePing(14.55, 120.95, geohash7, VehicleType.Jeepney));

        var result = await _handler.HandleAsync(14.5, 120.9, 14.6, 121.0, null);

        Assert.True(result.IsSuccess);
        var tile = Assert.Single(result.Tiles);
        // HeatmapTileDto only has Geohash7, DemandCount, VehicleType — no PII fields
        Assert.Equal(geohash7, tile.Geohash7);
        Assert.Equal(1, tile.DemandCount);
        Assert.Equal("Jeepney", tile.VehicleType);
    }

    [Fact]
    public async Task HandleAsync_MultipleGeohash7Cells_AggregatesSeparately()
    {
        _repository.AddPing(CreatePing(14.55, 120.95, "wdw5ny1", VehicleType.Jeepney));
        _repository.AddPing(CreatePing(14.55, 120.95, "wdw5ny1", VehicleType.Jeepney));
        _repository.AddPing(CreatePing(14.56, 120.96, "wdw5ny2", VehicleType.Jeepney));

        var result = await _handler.HandleAsync(14.5, 120.9, 14.6, 121.0, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Tiles.Count);
        Assert.Contains(result.Tiles, t => t.Geohash7 == "wdw5ny1" && t.DemandCount == 2);
        Assert.Contains(result.Tiles, t => t.Geohash7 == "wdw5ny2" && t.DemandCount == 1);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static DemandPing CreatePing(double lat, double lng, string geohash7, VehicleType vehicleType)
    {
        var geohash5 = geohash7[..5];
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

    // ─── Fake Repository ──────────────────────────────────────────────────

    /// <summary>
    /// In-memory fake of IDemandPingRepository for unit testing.
    /// Only implements QueryByBboxAsync which is used by GetTilesHandler.
    /// </summary>
    private sealed class FakeDemandPingRepository : IDemandPingRepository
    {
        private readonly List<DemandPing> _pings = new();

        public void AddPing(DemandPing ping) => _pings.Add(ping);

        public Task<IReadOnlyList<DemandPing>> QueryByBboxAsync(
            IEnumerable<string> geohash5Cells,
            CancellationToken cancellationToken = default)
        {
            var cells = geohash5Cells.ToHashSet();
            var now = DateTime.UtcNow;
            var result = _pings
                .Where(p => cells.Contains(p.Geohash5) && p.ExpiresAt > now)
                .ToList();
            return Task.FromResult<IReadOnlyList<DemandPing>>(result);
        }

        // ─── Unused interface members ─────────────────────────────────────

        public Task<bool> PutPingAsync(DemandPing ping, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<DemandPing>> GetActivePingsByGeohash5Async(string geohash5, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DemandPing>>(new List<DemandPing>());

        public Task<DemandPing?> GetActivePingByCommuterAsync(Guid commuterId, CancellationToken cancellationToken = default)
            => Task.FromResult<DemandPing?>(null);

        public Task DeletePingAsync(Guid commuterId, Guid pingId, string geohash5, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<DemandPing?> GetItemAsync(string pk, string sk, CancellationToken cancellationToken = default)
            => Task.FromResult<DemandPing?>(null);

        public Task<bool> PutItemAsync(DemandPing entity, bool conditionalOnNotExists = false, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<DemandPing>> QueryAsync(string pk, string? skPrefix = null, bool scanForward = true, int? limit = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DemandPing>>(new List<DemandPing>());

        public Task<IReadOnlyList<DemandPing>> QueryByIndexAsync(string indexName, string indexPk, string? indexSkPrefix = null, bool scanForward = true, int? limit = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DemandPing>>(new List<DemandPing>());

        public Task DeleteAsync(string pk, string sk, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
