namespace BiyaHero.Api.Domain;

/// <summary>
/// A single geographic point within a Route or RouteRevision.
/// Waypoints are value objects composed into Route/RouteRevision aggregates.
/// </summary>
public class Waypoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int SequenceOrder { get; set; }
    public string? Label { get; set; }

    public Waypoint() { }

    public Waypoint(double latitude, double longitude, int sequenceOrder, string? label = null)
    {
        Latitude = latitude;
        Longitude = longitude;
        SequenceOrder = sequenceOrder;
        Label = label;
    }

    /// <summary>
    /// Serialize this waypoint to a dictionary for JSON output.
    /// </summary>
    public Dictionary<string, object?> Serialize()
    {
        return new Dictionary<string, object?>
        {
            ["latitude"] = Latitude,
            ["longitude"] = Longitude,
            ["sequenceOrder"] = SequenceOrder,
            ["label"] = Label
        };
    }

    /// <summary>
    /// Parse a serialized dictionary back into a Waypoint instance.
    /// </summary>
    public static Waypoint Parse(Dictionary<string, object?> data)
    {
        var latitude = Convert.ToDouble(data["latitude"]);
        var longitude = Convert.ToDouble(data["longitude"]);
        var sequenceOrder = Convert.ToInt32(data["sequenceOrder"]);
        var label = data.TryGetValue("label", out var labelVal) ? labelVal?.ToString() : null;

        return new Waypoint(latitude, longitude, sequenceOrder, label);
    }
}
