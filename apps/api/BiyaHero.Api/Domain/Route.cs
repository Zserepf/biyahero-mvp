using System.Text.Json;

namespace BiyaHero.Api.Domain;

/// <summary>
/// A named, ordered sequence of geospatial waypoints describing a PUV path.
/// Aggregates its Waypoints; the repository layer handles splitting across storage tables.
/// Serialize produces the JSON shape required by Req 1.9; Parse is the inverse (Req 1.10).
/// Round-trip property: Parse(Serialize(route)).Serialize() == route.Serialize() (Req 1.11).
/// </summary>
public class Route : BaseDomain
{
    public string Name { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public RouteStatus Status { get; set; }
    public Guid CreatedBy { get; set; }
    public decimal BaseFare { get; set; }
    public List<Waypoint> Waypoints { get; set; } = new();

    /// <summary>
    /// Number of "still accurate" votes. Populated at query time, not persisted on the Route row.
    /// </summary>
    public int StillAccurateCount { get; set; }

    /// <summary>
    /// Number of "no longer accurate" votes. Populated at query time, not persisted on the Route row.
    /// </summary>
    public int NoLongerAccurateCount { get; set; }

    public Route() : base() { }

    public Route(
        Guid id,
        DateTime createdAt,
        DateTime updatedAt,
        string name,
        VehicleType vehicleType,
        RouteStatus status,
        Guid createdBy,
        decimal baseFare,
        List<Waypoint> waypoints,
        int stillAccurateCount = 0,
        int noLongerAccurateCount = 0)
        : base(id, createdAt, updatedAt)
    {
        Name = name;
        VehicleType = vehicleType;
        Status = status;
        CreatedBy = createdBy;
        BaseFare = baseFare;
        Waypoints = waypoints;
        StillAccurateCount = stillAccurateCount;
        NoLongerAccurateCount = noLongerAccurateCount;
    }

    /// <summary>
    /// Serialize this Route to a JSON-compatible dictionary.
    /// Produces the shape required by Req 1.9: route ID, name, vehicle type,
    /// ordered waypoints, base fare, status, and verification vote counts.
    /// </summary>
    public override Dictionary<string, object?> Serialize()
    {
        var dict = base.Serialize();
        dict["name"] = Name;
        dict["vehicleType"] = VehicleType.ToString();
        dict["status"] = Status.ToString();
        dict["createdBy"] = CreatedBy.ToString();
        dict["baseFare"] = BaseFare;
        dict["waypoints"] = Waypoints
            .OrderBy(w => w.SequenceOrder)
            .Select(w => w.Serialize())
            .ToList();
        dict["stillAccurateCount"] = StillAccurateCount;
        dict["noLongerAccurateCount"] = NoLongerAccurateCount;
        return dict;
    }

    /// <summary>
    /// Parse a serialized dictionary back into a Route instance.
    /// This is the inverse of Serialize() and enables round-trip verification (Req 1.10, 1.11).
    /// </summary>
    public static Route Parse(Dictionary<string, object?> data)
    {
        var id = Guid.Parse(data["id"]?.ToString() ?? throw new ArgumentException("Missing id"));
        var createdAt = DateTime.Parse(data["createdAt"]?.ToString() ?? throw new ArgumentException("Missing createdAt"));
        var updatedAt = DateTime.Parse(data["updatedAt"]?.ToString() ?? throw new ArgumentException("Missing updatedAt"));
        var name = data["name"]?.ToString() ?? string.Empty;
        var vehicleType = Enum.Parse<VehicleType>(data["vehicleType"]?.ToString() ?? throw new ArgumentException("Missing vehicleType"));
        var status = Enum.Parse<RouteStatus>(data["status"]?.ToString() ?? throw new ArgumentException("Missing status"));
        var createdBy = Guid.Parse(data["createdBy"]?.ToString() ?? throw new ArgumentException("Missing createdBy"));
        var baseFare = Convert.ToDecimal(data["baseFare"]);

        var stillAccurateCount = data.TryGetValue("stillAccurateCount", out var sacVal) && sacVal != null
            ? Convert.ToInt32(sacVal)
            : 0;
        var noLongerAccurateCount = data.TryGetValue("noLongerAccurateCount", out var nlacVal) && nlacVal != null
            ? Convert.ToInt32(nlacVal)
            : 0;

        var waypoints = new List<Waypoint>();
        if (data.TryGetValue("waypoints", out var waypointsVal) && waypointsVal is IEnumerable<object> waypointsList)
        {
            foreach (var wp in waypointsList)
            {
                if (wp is Dictionary<string, object?> wpDict)
                {
                    waypoints.Add(Waypoint.Parse(wpDict));
                }
                else if (wp is JsonElement jsonElement)
                {
                    var wpDict2 = JsonElementToDictionary(jsonElement);
                    waypoints.Add(Waypoint.Parse(wpDict2));
                }
            }
        }

        return new Route(id, createdAt, updatedAt, name, vehicleType, status, createdBy, baseFare, waypoints, stillAccurateCount, noLongerAccurateCount);
    }

    /// <summary>
    /// Parse a Route from a JSON string.
    /// </summary>
    public static Route ParseFromJson(string json)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, RouteJsonContext.Default.DictionaryStringObject)
            ?? throw new ArgumentException("Invalid JSON");
        return Parse(NormalizeJsonDictionary(dict));
    }

    private static Dictionary<string, object?> NormalizeJsonDictionary(Dictionary<string, object?> raw)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in raw)
        {
            if (kvp.Value is JsonElement element)
            {
                result[kvp.Key] = ConvertJsonElement(element);
            }
            else
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var intVal))
                    return intVal;
                if (element.TryGetDecimal(out var decVal))
                    return decVal;
                return element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    var converted = ConvertJsonElement(item);
                    if (converted != null) list.Add(converted);
                }
                return list;
            case JsonValueKind.Object:
                return JsonElementToDictionary(element);
            default:
                return element.ToString();
        }
    }

    private static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = ConvertJsonElement(prop.Value);
        }
        return dict;
    }
}

/// <summary>
/// AOT-compatible JSON serializer context for Route serialization.
/// </summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, object?>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(List<Dictionary<string, object?>>))]
public partial class RouteJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
