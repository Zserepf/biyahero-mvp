using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Features.Heatmap.GetTiles;

/// <summary>
/// Business logic for GET /v1/heatmap/tiles.
/// Validates bbox parameters, determines which geohash5 partitions to scan,
/// queries DynamoDB for active demand pings, and aggregates by geohash7 + vehicle type.
/// 
/// No authentication required — public data for drivers (Requirement 4.2, 4.6).
/// No PII is ever included in the response — only geohash7, demand count, and vehicle type.
/// Target: p95 ≤ 500ms (Requirement 4.2).
/// </summary>
public sealed class GetTilesHandler
{
    private readonly IDemandPingRepository _demandPingRepository;

    public GetTilesHandler(IDemandPingRepository demandPingRepository)
    {
        _demandPingRepository = demandPingRepository;
    }

    /// <summary>
    /// Handles the heatmap tiles request.
    /// 1. Validates bbox coordinates
    /// 2. Computes geohash5 cells covering the bbox (for DynamoDB partition queries)
    /// 3. Queries active demand pings from those partitions
    /// 4. Aggregates pings by geohash7 + vehicle type
    /// 5. Returns tiles with no PII
    /// </summary>
    public async Task<GetTilesResult> HandleAsync(
        double minLat,
        double minLng,
        double maxLat,
        double maxLng,
        string? vehicleType,
        CancellationToken cancellationToken = default)
    {
        // Validate bbox ranges
        if (minLat < -90 || minLat > 90 || maxLat < -90 || maxLat > 90)
        {
            return GetTilesResult.ValidationError("Latitude must be between -90 and 90 degrees.");
        }

        if (minLng < -180 || minLng > 180 || maxLng < -180 || maxLng > 180)
        {
            return GetTilesResult.ValidationError("Longitude must be between -180 and 180 degrees.");
        }

        if (minLat > maxLat)
        {
            return GetTilesResult.ValidationError("minLat must be less than or equal to maxLat.");
        }

        if (minLng > maxLng)
        {
            return GetTilesResult.ValidationError("minLng must be less than or equal to maxLng.");
        }

        // Validate vehicle type if provided
        VehicleType? parsedVehicleType = null;
        if (!string.IsNullOrWhiteSpace(vehicleType))
        {
            if (!Enum.TryParse<VehicleType>(vehicleType, ignoreCase: true, out var vt))
            {
                return GetTilesResult.ValidationError($"Unsupported vehicle type: '{vehicleType}'.");
            }
            parsedVehicleType = vt;
        }

        // Compute geohash5 cells covering the bbox for DynamoDB partition routing.
        // Precision 5 (~5 km cells) is the partition key granularity.
        var geohash5Cells = GeohashEncoder.GetGeohashesInBbox(minLat, minLng, maxLat, maxLng, precision: 5);

        // Query active demand pings from all relevant geohash5 partitions in parallel.
        // The repository filters out expired pings (TTL-based).
        var activePings = await _demandPingRepository.QueryByBboxAsync(geohash5Cells, cancellationToken);

        // Filter pings to only those within the requested bbox (geohash5 cells may extend
        // beyond the exact bbox boundary) and optionally by vehicle type.
        var filteredPings = activePings.Where(p =>
            p.Latitude >= minLat && p.Latitude <= maxLat &&
            p.Longitude >= minLng && p.Longitude <= maxLng);

        if (parsedVehicleType is not null)
        {
            filteredPings = filteredPings.Where(p => p.VehicleType == parsedVehicleType.Value);
        }

        // Aggregate by geohash7 + vehicle type — NO PII (no commuter IDs, names, etc.)
        var tiles = filteredPings
            .GroupBy(p => (p.Geohash7, p.VehicleType))
            .Select(g => new HeatmapTileDto(
                Geohash7: g.Key.Geohash7,
                DemandCount: g.Count(),
                VehicleType: g.Key.VehicleType.ToString()))
            .ToList();

        return GetTilesResult.Success(tiles);
    }
}

/// <summary>
/// Result wrapper for the GetTiles operation.
/// </summary>
public sealed class GetTilesResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public List<HeatmapTileDto> Tiles { get; }

    private GetTilesResult(bool isSuccess, List<HeatmapTileDto>? tiles, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Tiles = tiles ?? new List<HeatmapTileDto>();
        ErrorMessage = errorMessage;
    }

    public static GetTilesResult Success(List<HeatmapTileDto> tiles) => new(true, tiles, null);
    public static GetTilesResult ValidationError(string message) => new(false, null, message);
}
