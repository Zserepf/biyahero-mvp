using System.Text.Json;
using System.Text.Json.Serialization;
using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Features.Fare;

/// <summary>
/// Represents a single row in the fare_matrices table — the LTFRB-published fare rules
/// for a specific vehicle type at a specific version/effective date.
/// </summary>
public class FareMatrix : BaseDomain
{
    /// <summary>
    /// Version identifier for this fare matrix (e.g., "v1", "v2").
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// The date/time from which this matrix becomes effective.
    /// The loader picks the latest effective_at that is not in the future.
    /// </summary>
    public DateTime EffectiveAt { get; set; }

    /// <summary>
    /// The vehicle type this matrix applies to (e.g., "jeepney", "uv_express", "bus", "tricycle").
    /// </summary>
    public string VehicleType { get; set; } = string.Empty;

    /// <summary>
    /// The minimum fare in centavos for this vehicle type.
    /// </summary>
    public int MinFareCentavos { get; set; }

    /// <summary>
    /// The distance in kilometers covered by the minimum fare.
    /// </summary>
    public double MinFareKm { get; set; }

    /// <summary>
    /// The per-kilometer increment in centavos beyond the minimum-fare threshold.
    /// </summary>
    public int PerKmCentavos { get; set; }

    /// <summary>
    /// Discount percentages by category. Keys: "regular", "student", "senior", "pwd".
    /// Values are the discount percentage as a whole number (e.g., 20 means 20% off).
    /// </summary>
    public Dictionary<string, int> DiscountPercentByCategory { get; set; } = new();

    public FareMatrix() : base() { }

    /// <summary>
    /// Convenience constructor for creating a FareMatrix without specifying base domain fields.
    /// Useful for tests and JSON deserialization where Id/CreatedAt/UpdatedAt are auto-generated.
    /// </summary>
    public FareMatrix(
        string version,
        string vehicleType,
        int minFareCentavos,
        double minFareKm,
        int perKmCentavos,
        Dictionary<string, int> discountPercentByCategory)
        : base()
    {
        Version = version;
        EffectiveAt = DateTime.UtcNow;
        VehicleType = vehicleType;
        MinFareCentavos = minFareCentavos;
        MinFareKm = minFareKm;
        PerKmCentavos = perKmCentavos;
        DiscountPercentByCategory = discountPercentByCategory;
    }

    /// <summary>
    /// Full constructor including base domain fields and effective date.
    /// Used when loading from the database.
    /// </summary>
    public FareMatrix(
        Guid id,
        DateTime createdAt,
        DateTime updatedAt,
        string version,
        DateTime effectiveAt,
        string vehicleType,
        int minFareCentavos,
        double minFareKm,
        int perKmCentavos,
        Dictionary<string, int> discountPercentByCategory)
        : base(id, createdAt, updatedAt)
    {
        Version = version;
        EffectiveAt = effectiveAt;
        VehicleType = vehicleType;
        MinFareCentavos = minFareCentavos;
        MinFareKm = minFareKm;
        PerKmCentavos = perKmCentavos;
        DiscountPercentByCategory = discountPercentByCategory;
    }

    public override Dictionary<string, object?> Serialize()
    {
        var dict = base.Serialize();
        dict["version"] = Version;
        dict["effectiveAt"] = EffectiveAt.ToString("o");
        dict["vehicleType"] = VehicleType;
        dict["minFareCentavos"] = MinFareCentavos;
        dict["minFareKm"] = MinFareKm;
        dict["perKmCentavos"] = PerKmCentavos;
        dict["discountPercentByCategory"] = DiscountPercentByCategory;
        return dict;
    }

    /// <summary>
    /// Parse a serialized dictionary back into a FareMatrix instance.
    /// </summary>
    public static FareMatrix Parse(Dictionary<string, object?> data)
    {
        var id = Guid.Parse(data["id"]?.ToString() ?? throw new ArgumentException("Missing id"));
        var createdAt = DateTime.Parse(data["createdAt"]?.ToString() ?? throw new ArgumentException("Missing createdAt"));
        var updatedAt = DateTime.Parse(data["updatedAt"]?.ToString() ?? throw new ArgumentException("Missing updatedAt"));
        var version = data["version"]?.ToString() ?? throw new ArgumentException("Missing version");
        var effectiveAt = DateTime.Parse(data["effectiveAt"]?.ToString() ?? throw new ArgumentException("Missing effectiveAt"));
        var vehicleType = data["vehicleType"]?.ToString() ?? throw new ArgumentException("Missing vehicleType");
        var minFareCentavos = Convert.ToInt32(data["minFareCentavos"]);
        var minFareKm = Convert.ToDouble(data["minFareKm"]);
        var perKmCentavos = Convert.ToInt32(data["perKmCentavos"]);

        var discountDict = new Dictionary<string, int>();
        if (data.TryGetValue("discountPercentByCategory", out var discountObj) && discountObj is Dictionary<string, int> typed)
        {
            discountDict = typed;
        }
        else if (discountObj is JsonElement jsonElement)
        {
            foreach (var prop in jsonElement.EnumerateObject())
            {
                discountDict[prop.Name] = prop.Value.GetInt32();
            }
        }

        return new FareMatrix(id, createdAt, updatedAt, version, effectiveAt, vehicleType,
            minFareCentavos, minFareKm, perKmCentavos, discountDict);
    }
}

/// <summary>
/// JSON-serializable model for the fare-matrix.json config file.
/// Used by the FareMatrixLoader to load fare configurations from a JSON file
/// when the database is not available or as a fallback/seed source.
/// </summary>
public class FareMatrixConfig
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("effectiveDate")]
    public string EffectiveDate { get; set; } = string.Empty;

    [JsonPropertyName("matrices")]
    public List<FareMatrixEntry> Matrices { get; set; } = new();
}

/// <summary>
/// A single vehicle type entry within the fare matrix config file.
/// </summary>
public class FareMatrixEntry
{
    [JsonPropertyName("vehicleType")]
    public string VehicleType { get; set; } = string.Empty;

    [JsonPropertyName("minFareCentavos")]
    public int MinFareCentavos { get; set; }

    [JsonPropertyName("minFareKm")]
    public double MinFareKm { get; set; }

    [JsonPropertyName("perKmCentavos")]
    public int PerKmCentavos { get; set; }

    [JsonPropertyName("discountPercentByCategory")]
    public Dictionary<string, int> DiscountPercentByCategory { get; set; } = new();
}

/// <summary>
/// AOT-compatible JSON serializer context for FareMatrix config deserialization.
/// </summary>
[JsonSerializable(typeof(FareMatrixConfig))]
[JsonSerializable(typeof(List<FareMatrixEntry>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
public partial class FareMatrixJsonContext : JsonSerializerContext
{
}
