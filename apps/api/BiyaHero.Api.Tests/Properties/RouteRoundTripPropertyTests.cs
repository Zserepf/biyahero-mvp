using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Tests.Properties;

/// <summary>
/// Property-based tests for Route serialization round-trip.
/// Validates: Requirements 1.10, 1.11
/// Feature: biyahero-mvp, Property 1: Round-trip Route serialization
/// </summary>
public class RouteRoundTripPropertyTests
{
    /// <summary>
    /// Custom Arbitrary for generating valid Waypoint instances.
    /// Constrains latitude to Philippines bbox (4.5–21.5 N) and longitude (116–127 E).
    /// </summary>
    private static Arbitrary<Waypoint> ArbWaypoint()
    {
        return (from lat in Gen.Choose(4500, 21500).Select(x => x / 1000.0)
                from lng in Gen.Choose(116000, 127000).Select(x => x / 1000.0)
                from seq in Gen.Choose(0, 100)
                from hasLabel in Arb.Generate<bool>()
                from label in Gen.Elements("Start", "Stop A", "Stop B", "Terminal", "Corner", "Market", "Church")
                select new Waypoint(lat, lng, seq, hasLabel ? label : null))
            .ToArbitrary();
    }

    /// <summary>
    /// Custom Arbitrary for generating valid Route instances with random data.
    /// </summary>
    private static Arbitrary<Route> ArbRoute()
    {
        var vehicleTypes = Enum.GetValues<VehicleType>();
        var routeStatuses = Enum.GetValues<RouteStatus>();

        return (from id in Arb.Generate<Guid>()
                from createdAt in Gen.Choose(2020, 2024).SelectMany(y =>
                    Gen.Choose(1, 12).SelectMany(m =>
                        Gen.Choose(1, 28).Select(d =>
                            new DateTime(y, m, d, 12, 0, 0, DateTimeKind.Utc))))
                from updatedAt in Gen.Choose(0, 365).Select(days => createdAt.AddDays(days))
                from name in Gen.Elements(
                    "Cubao-Monumento", "EDSA Carousel", "Quiapo-Divisoria",
                    "Fairview-Philcoa", "Antipolo-Cubao", "SM North-Trinoma",
                    "Baclaran-Pasay", "Marikina-Katipunan")
                from vehicleType in Gen.Elements(vehicleTypes)
                from status in Gen.Elements(routeStatuses)
                from createdBy in Arb.Generate<Guid>()
                from baseFare in Gen.Choose(800, 5000).Select(x => (decimal)x / 100m)
                from waypointCount in Gen.Choose(2, 10)
                from waypoints in Gen.ListOf(waypointCount, ArbWaypoint().Generator)
                from stillAccurate in Gen.Choose(0, 50)
                from noLongerAccurate in Gen.Choose(0, 20)
                let orderedWaypoints = waypoints.Select((w, i) => new Waypoint(w.Latitude, w.Longitude, i, w.Label)).ToList()
                select new Route(
                    id, createdAt, updatedAt, name, vehicleType, status,
                    createdBy, baseFare, orderedWaypoints, stillAccurate, noLongerAccurate))
            .ToArbitrary();
    }

    /// <summary>
    /// **Validates: Requirements 1.10, 1.11**
    /// 
    /// Property 1: Round-trip Route serialization.
    /// For all valid Route entities, serializing, parsing, and re-serializing
    /// produces a dictionary semantically equivalent to the original serialization.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "biyahero-mvp")]
    [Trait("Property", "Property 1: Round-trip Route serialization")]
    public Property RoundTrip_Route_Serialization()
    {
        return Prop.ForAll(ArbRoute(), route =>
        {
            // Step 1: Serialize the original Route
            var serialized1 = route.Serialize();

            // Step 2: Parse it back into a Route
            var parsed = Route.Parse(serialized1);

            // Step 3: Re-serialize the parsed Route
            var serialized2 = parsed.Serialize();

            // Step 4: Assert the two serialized dictionaries are equivalent
            return DictionariesAreEquivalent(serialized1, serialized2)
                .Label("Serialized dictionaries should be equivalent after round-trip");
        });
    }

    /// <summary>
    /// Compares two dictionaries for semantic equivalence, handling nested structures.
    /// </summary>
    private static bool DictionariesAreEquivalent(
        Dictionary<string, object?> dict1,
        Dictionary<string, object?> dict2)
    {
        if (dict1.Count != dict2.Count)
            return false;

        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var val2))
                return false;

            if (!ValuesAreEquivalent(kvp.Value, val2))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Compares two values for semantic equivalence, handling lists, dictionaries, and primitives.
    /// </summary>
    private static bool ValuesAreEquivalent(object? val1, object? val2)
    {
        if (val1 is null && val2 is null)
            return true;
        if (val1 is null || val2 is null)
            return false;

        // Handle nested dictionaries
        if (val1 is Dictionary<string, object?> d1 && val2 is Dictionary<string, object?> d2)
            return DictionariesAreEquivalent(d1, d2);

        // Handle lists (e.g., waypoints)
        if (val1 is IList<object?> list1 && val2 is IList<object?> list2)
        {
            if (list1.Count != list2.Count)
                return false;
            for (int i = 0; i < list1.Count; i++)
            {
                if (!ValuesAreEquivalent(list1[i], list2[i]))
                    return false;
            }
            return true;
        }

        // Handle List<Dictionary<string, object?>> from Serialize
        if (val1 is IEnumerable<object> enum1 && val2 is IEnumerable<object> enum2)
        {
            var l1 = enum1.ToList();
            var l2 = enum2.ToList();
            if (l1.Count != l2.Count)
                return false;
            for (int i = 0; i < l1.Count; i++)
            {
                if (!ValuesAreEquivalent(l1[i], l2[i]))
                    return false;
            }
            return true;
        }

        // Compare numeric values with tolerance for decimal/double conversions
        if (IsNumeric(val1) && IsNumeric(val2))
        {
            var d1Val = Convert.ToDouble(val1);
            var d2Val = Convert.ToDouble(val2);
            return Math.Abs(d1Val - d2Val) < 0.0001;
        }

        // String comparison (handles DateTime round-trip formatting)
        return val1.ToString() == val2.ToString();
    }

    private static bool IsNumeric(object? value)
    {
        return value is int or long or float or double or decimal;
    }
}
