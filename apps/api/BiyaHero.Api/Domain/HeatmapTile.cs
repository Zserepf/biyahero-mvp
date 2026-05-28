namespace BiyaHero.Api.Domain;

/// <summary>
/// A spatial bucket representing aggregated demand at geohash precision 7 (~150 m cell).
/// Contains ONLY the geohash, demand count, and vehicle type — NO personally identifying
/// information (no commuter ID, name, email, or device ID) per Requirement 4.6.
/// Returned to Drivers as part of heatmap subscription updates.
/// </summary>
public class HeatmapTile : BaseDomain
{
    /// <summary>Geohash precision 7 identifying this tile (~150 m cell).</summary>
    public string Geohash7 { get; set; } = string.Empty;

    /// <summary>Number of active demand pings within this tile.</summary>
    public int DemandCount { get; set; }

    /// <summary>The vehicle type this tile aggregates demand for.</summary>
    public VehicleType VehicleType { get; set; }

    public HeatmapTile() : base() { }

    public HeatmapTile(
        Guid id,
        DateTime createdAt,
        DateTime updatedAt,
        string geohash7,
        int demandCount,
        VehicleType vehicleType)
        : base(id, createdAt, updatedAt)
    {
        Geohash7 = geohash7;
        DemandCount = demandCount;
        VehicleType = vehicleType;
    }

    /// <summary>
    /// Serialize this HeatmapTile to a JSON-compatible dictionary.
    /// Intentionally excludes any PII — only geohash, count, and vehicle type.
    /// </summary>
    public override Dictionary<string, object?> Serialize()
    {
        var dict = base.Serialize();
        dict["geohash7"] = Geohash7;
        dict["demandCount"] = DemandCount;
        dict["vehicleType"] = VehicleType.ToString();
        return dict;
    }

    /// <summary>
    /// Parse a serialized dictionary back into a HeatmapTile instance.
    /// Inverse of Serialize() for round-trip verification.
    /// </summary>
    public static HeatmapTile Parse(Dictionary<string, object?> data)
    {
        var id = Guid.Parse(data["id"]?.ToString() ?? throw new ArgumentException("Missing id"));
        var createdAt = DateTime.Parse(data["createdAt"]?.ToString() ?? throw new ArgumentException("Missing createdAt"));
        var updatedAt = DateTime.Parse(data["updatedAt"]?.ToString() ?? throw new ArgumentException("Missing updatedAt"));
        var geohash7 = data["geohash7"]?.ToString() ?? string.Empty;
        var demandCount = int.Parse(data["demandCount"]?.ToString() ?? throw new ArgumentException("Missing demandCount"));
        var vehicleType = Enum.Parse<VehicleType>(data["vehicleType"]?.ToString() ?? throw new ArgumentException("Missing vehicleType"));

        return new HeatmapTile(id, createdAt, updatedAt, geohash7, demandCount, vehicleType);
    }
}
