namespace BiyaHero.Api.Features.Heatmap.GetTiles;

/// <summary>
/// Response DTO for a single heatmap tile.
/// Contains only geohash, demand count, and vehicle type — no PII (Requirement 4.6).
/// </summary>
public record HeatmapTileDto(
    string Geohash7,
    int DemandCount,
    string VehicleType);
