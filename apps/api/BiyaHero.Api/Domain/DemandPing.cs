namespace BiyaHero.Api.Domain;

/// <summary>
/// A short-lived geolocation event submitted by a Commuter indicating
/// they are waiting for a ride at a specific coordinate.
/// Persisted to DynamoDB with a 5-minute TTL.
/// Partition key uses Geohash5 for coarse spatial bucketing;
/// Geohash7 is the tile resolution used in heatmap aggregation.
/// </summary>
public class DemandPing : BaseDomain
{
    /// <summary>The commuter who submitted this ping.</summary>
    public Guid CommuterId { get; set; }

    /// <summary>Latitude of the commuter's location (WGS84).</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude of the commuter's location (WGS84).</summary>
    public double Longitude { get; set; }

    /// <summary>Geohash precision 5 (~5 km cell) used as the DynamoDB partition key.</summary>
    public string Geohash5 { get; set; } = string.Empty;

    /// <summary>Geohash precision 7 (~150 m cell) used for heatmap tile resolution.</summary>
    public string Geohash7 { get; set; } = string.Empty;

    /// <summary>The type of vehicle the commuter is waiting for.</summary>
    public VehicleType VehicleType { get; set; }

    /// <summary>When this ping expires (5-minute TTL from submission).</summary>
    public DateTime ExpiresAt { get; set; }

    public DemandPing() : base() { }

    public DemandPing(
        Guid id,
        DateTime createdAt,
        DateTime updatedAt,
        Guid commuterId,
        double latitude,
        double longitude,
        string geohash5,
        string geohash7,
        VehicleType vehicleType,
        DateTime expiresAt)
        : base(id, createdAt, updatedAt)
    {
        CommuterId = commuterId;
        Latitude = latitude;
        Longitude = longitude;
        Geohash5 = geohash5;
        Geohash7 = geohash7;
        VehicleType = vehicleType;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Serialize this DemandPing to a JSON-compatible dictionary.
    /// </summary>
    public override Dictionary<string, object?> Serialize()
    {
        var dict = base.Serialize();
        dict["commuterId"] = CommuterId.ToString();
        dict["latitude"] = Latitude;
        dict["longitude"] = Longitude;
        dict["geohash5"] = Geohash5;
        dict["geohash7"] = Geohash7;
        dict["vehicleType"] = VehicleType.ToString();
        dict["expiresAt"] = ExpiresAt.ToString("o");
        return dict;
    }

    /// <summary>
    /// Parse a serialized dictionary back into a DemandPing instance.
    /// Inverse of Serialize() for round-trip verification.
    /// </summary>
    public static DemandPing Parse(Dictionary<string, object?> data)
    {
        var id = Guid.Parse(data["id"]?.ToString() ?? throw new ArgumentException("Missing id"));
        var createdAt = DateTime.Parse(data["createdAt"]?.ToString() ?? throw new ArgumentException("Missing createdAt"));
        var updatedAt = DateTime.Parse(data["updatedAt"]?.ToString() ?? throw new ArgumentException("Missing updatedAt"));
        var commuterId = Guid.Parse(data["commuterId"]?.ToString() ?? throw new ArgumentException("Missing commuterId"));
        var latitude = double.Parse(data["latitude"]?.ToString() ?? throw new ArgumentException("Missing latitude"));
        var longitude = double.Parse(data["longitude"]?.ToString() ?? throw new ArgumentException("Missing longitude"));
        var geohash5 = data["geohash5"]?.ToString() ?? string.Empty;
        var geohash7 = data["geohash7"]?.ToString() ?? string.Empty;
        var vehicleType = Enum.Parse<VehicleType>(data["vehicleType"]?.ToString() ?? throw new ArgumentException("Missing vehicleType"));
        var expiresAt = DateTime.Parse(data["expiresAt"]?.ToString() ?? throw new ArgumentException("Missing expiresAt"));

        return new DemandPing(id, createdAt, updatedAt, commuterId, latitude, longitude, geohash5, geohash7, vehicleType, expiresAt);
    }
}
