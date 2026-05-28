using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Tests.Domain;

/// <summary>
/// Unit tests for Route, Waypoint, RouteRevision, and RouteVote domain classes.
/// Validates: Requirements 1.9, 1.10, 1.11
/// </summary>
public class RouteDomainTests
{
    private static Route CreateSampleRoute()
    {
        return new Route(
            id: Guid.NewGuid(),
            createdAt: new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            updatedAt: new DateTime(2024, 6, 15, 11, 0, 0, DateTimeKind.Utc),
            name: "Cubao-Monumento Jeepney",
            vehicleType: VehicleType.Jeepney,
            status: RouteStatus.Unverified,
            createdBy: Guid.NewGuid(),
            baseFare: 13.00m,
            waypoints: new List<Waypoint>
            {
                new(14.6195, 121.0530, 1, "Cubao Terminal"),
                new(14.6350, 121.0450, 2, null),
                new(14.6570, 120.9840, 3, "Monumento")
            });
    }

    [Fact]
    public void Route_Serialize_ContainsAllRequiredFields()
    {
        var route = CreateSampleRoute();
        var serialized = route.Serialize();

        Assert.True(serialized.ContainsKey("id"));
        Assert.True(serialized.ContainsKey("name"));
        Assert.True(serialized.ContainsKey("vehicleType"));
        Assert.True(serialized.ContainsKey("status"));
        Assert.True(serialized.ContainsKey("createdBy"));
        Assert.True(serialized.ContainsKey("baseFare"));
        Assert.True(serialized.ContainsKey("waypoints"));
        Assert.True(serialized.ContainsKey("createdAt"));
        Assert.True(serialized.ContainsKey("updatedAt"));
        Assert.True(serialized.ContainsKey("stillAccurateCount"));
        Assert.True(serialized.ContainsKey("noLongerAccurateCount"));
    }

    [Fact]
    public void Route_Serialize_WaypointsAreOrderedBySequence()
    {
        var route = new Route(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            name: "Test Route",
            vehicleType: VehicleType.Bus,
            status: RouteStatus.Verified,
            createdBy: Guid.NewGuid(),
            baseFare: 15.00m,
            waypoints: new List<Waypoint>
            {
                new(14.60, 121.00, 3, "Third"),
                new(14.55, 121.05, 1, "First"),
                new(14.58, 121.02, 2, "Second")
            });

        var serialized = route.Serialize();
        var waypoints = (List<Dictionary<string, object?>>)serialized["waypoints"]!;

        Assert.Equal(1, Convert.ToInt32(waypoints[0]["sequenceOrder"]));
        Assert.Equal(2, Convert.ToInt32(waypoints[1]["sequenceOrder"]));
        Assert.Equal(3, Convert.ToInt32(waypoints[2]["sequenceOrder"]));
    }

    [Fact]
    public void Route_Serialize_ThenParse_ProducesEquivalentRoute()
    {
        var original = CreateSampleRoute();

        var serialized = original.Serialize();
        var parsed = Route.Parse(serialized);

        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.Name, parsed.Name);
        Assert.Equal(original.VehicleType, parsed.VehicleType);
        Assert.Equal(original.Status, parsed.Status);
        Assert.Equal(original.CreatedBy, parsed.CreatedBy);
        Assert.Equal(original.BaseFare, parsed.BaseFare);
        Assert.Equal(original.Waypoints.Count, parsed.Waypoints.Count);

        for (int i = 0; i < original.Waypoints.Count; i++)
        {
            var origWp = original.Waypoints.OrderBy(w => w.SequenceOrder).ToList()[i];
            var parsedWp = parsed.Waypoints.OrderBy(w => w.SequenceOrder).ToList()[i];
            Assert.Equal(origWp.Latitude, parsedWp.Latitude);
            Assert.Equal(origWp.Longitude, parsedWp.Longitude);
            Assert.Equal(origWp.SequenceOrder, parsedWp.SequenceOrder);
            Assert.Equal(origWp.Label, parsedWp.Label);
        }
    }

    [Fact]
    public void Route_RoundTrip_SerializeParseReserialize_ProducesSameOutput()
    {
        var original = CreateSampleRoute();

        var first = original.Serialize();
        var parsed = Route.Parse(first);
        var second = parsed.Serialize();

        Assert.Equal(first.Count, second.Count);
        foreach (var key in first.Keys)
        {
            if (key == "waypoints")
            {
                var firstWps = (List<Dictionary<string, object?>>)first["waypoints"]!;
                var secondWps = (List<Dictionary<string, object?>>)second["waypoints"]!;
                Assert.Equal(firstWps.Count, secondWps.Count);
                for (int i = 0; i < firstWps.Count; i++)
                {
                    Assert.Equal(firstWps[i]["latitude"]?.ToString(), secondWps[i]["latitude"]?.ToString());
                    Assert.Equal(firstWps[i]["longitude"]?.ToString(), secondWps[i]["longitude"]?.ToString());
                    Assert.Equal(firstWps[i]["sequenceOrder"]?.ToString(), secondWps[i]["sequenceOrder"]?.ToString());
                    Assert.Equal(firstWps[i]["label"]?.ToString(), secondWps[i]["label"]?.ToString());
                }
            }
            else
            {
                Assert.Equal(first[key]?.ToString(), second[key]?.ToString());
            }
        }
    }

    [Fact]
    public void Route_ParseFromJson_WorksCorrectly()
    {
        var original = CreateSampleRoute();
        var json = original.SerializeToJson();
        var parsed = Route.ParseFromJson(json);

        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.Name, parsed.Name);
        Assert.Equal(original.VehicleType, parsed.VehicleType);
        Assert.Equal(original.Status, parsed.Status);
        Assert.Equal(original.BaseFare, parsed.BaseFare);
        Assert.Equal(original.Waypoints.Count, parsed.Waypoints.Count);
    }

    [Fact]
    public void Waypoint_Serialize_ThenParse_RoundTrips()
    {
        var original = new Waypoint(14.5995, 120.9842, 1, "Rizal Park");

        var serialized = original.Serialize();
        var parsed = Waypoint.Parse(serialized);

        Assert.Equal(original.Latitude, parsed.Latitude);
        Assert.Equal(original.Longitude, parsed.Longitude);
        Assert.Equal(original.SequenceOrder, parsed.SequenceOrder);
        Assert.Equal(original.Label, parsed.Label);
    }

    [Fact]
    public void Waypoint_WithNullLabel_SerializesCorrectly()
    {
        var wp = new Waypoint(14.60, 121.00, 2, null);

        var serialized = wp.Serialize();
        var parsed = Waypoint.Parse(serialized);

        Assert.Null(parsed.Label);
    }

    [Fact]
    public void RouteRevision_Serialize_ThenParse_RoundTrips()
    {
        var original = new RouteRevision(
            id: Guid.NewGuid(),
            createdAt: new DateTime(2024, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            updatedAt: new DateTime(2024, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            routeId: Guid.NewGuid(),
            submittedBy: Guid.NewGuid(),
            status: RevisionStatus.Pending,
            waypoints: new List<Waypoint>
            {
                new(14.55, 121.00, 1, "Start"),
                new(14.60, 121.05, 2, "End")
            });

        var serialized = original.Serialize();
        var parsed = RouteRevision.Parse(serialized);

        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.RouteId, parsed.RouteId);
        Assert.Equal(original.SubmittedBy, parsed.SubmittedBy);
        Assert.Equal(original.Status, parsed.Status);
        Assert.Equal(original.Waypoints.Count, parsed.Waypoints.Count);
    }

    [Fact]
    public void RouteVote_Serialize_ThenParse_RoundTrips()
    {
        var original = new RouteVote(
            id: Guid.NewGuid(),
            createdAt: new DateTime(2024, 7, 2, 10, 0, 0, DateTimeKind.Utc),
            updatedAt: new DateTime(2024, 7, 2, 10, 0, 0, DateTimeKind.Utc),
            routeId: Guid.NewGuid(),
            voterId: Guid.NewGuid(),
            kind: VoteKind.StillAccurate,
            timestamp: new DateTime(2024, 7, 2, 10, 0, 0, DateTimeKind.Utc));

        var serialized = original.Serialize();
        var parsed = RouteVote.Parse(serialized);

        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.RouteId, parsed.RouteId);
        Assert.Equal(original.VoterId, parsed.VoterId);
        Assert.Equal(original.Kind, parsed.Kind);
        Assert.Equal(original.Timestamp, parsed.Timestamp);
    }

    [Fact]
    public void RouteVote_NoLongerAccurate_SerializesCorrectly()
    {
        var vote = new RouteVote(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            routeId: Guid.NewGuid(),
            voterId: Guid.NewGuid(),
            kind: VoteKind.NoLongerAccurate,
            timestamp: DateTime.UtcNow);

        var serialized = vote.Serialize();

        Assert.Equal("NoLongerAccurate", serialized["kind"]?.ToString());
    }

    [Fact]
    public void Route_AllVehicleTypes_SerializeCorrectly()
    {
        foreach (var vehicleType in Enum.GetValues<VehicleType>())
        {
            var route = new Route
            {
                Name = "Test",
                VehicleType = vehicleType,
                Status = RouteStatus.Unverified,
                CreatedBy = Guid.NewGuid(),
                BaseFare = 10m,
                Waypoints = new List<Waypoint>
                {
                    new(14.5, 121.0, 1),
                    new(14.6, 121.1, 2)
                }
            };

            var serialized = route.Serialize();
            var parsed = Route.Parse(serialized);

            Assert.Equal(vehicleType, parsed.VehicleType);
        }
    }

    [Fact]
    public void Route_VoteCounts_SerializeAndParse_RoundTrips()
    {
        var route = new Route(
            id: Guid.NewGuid(),
            createdAt: new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            updatedAt: new DateTime(2024, 6, 15, 11, 0, 0, DateTimeKind.Utc),
            name: "Voted Route",
            vehicleType: VehicleType.Jeepney,
            status: RouteStatus.Verified,
            createdBy: Guid.NewGuid(),
            baseFare: 13.00m,
            waypoints: new List<Waypoint>
            {
                new(14.6195, 121.0530, 1, "Start"),
                new(14.6570, 120.9840, 2, "End")
            },
            stillAccurateCount: 42,
            noLongerAccurateCount: 7);

        var serialized = route.Serialize();
        Assert.Equal(42, Convert.ToInt32(serialized["stillAccurateCount"]));
        Assert.Equal(7, Convert.ToInt32(serialized["noLongerAccurateCount"]));

        var parsed = Route.Parse(serialized);
        Assert.Equal(42, parsed.StillAccurateCount);
        Assert.Equal(7, parsed.NoLongerAccurateCount);
    }

    [Fact]
    public void Route_DefaultVoteCounts_AreZero()
    {
        var route = new Route
        {
            Name = "New Route",
            VehicleType = VehicleType.Bus,
            Status = RouteStatus.Unverified,
            CreatedBy = Guid.NewGuid(),
            BaseFare = 15m,
            Waypoints = new List<Waypoint>
            {
                new(14.5, 121.0, 1),
                new(14.6, 121.1, 2)
            }
        };

        Assert.Equal(0, route.StillAccurateCount);
        Assert.Equal(0, route.NoLongerAccurateCount);

        var serialized = route.Serialize();
        Assert.Equal(0, Convert.ToInt32(serialized["stillAccurateCount"]));
        Assert.Equal(0, Convert.ToInt32(serialized["noLongerAccurateCount"]));
    }
}
