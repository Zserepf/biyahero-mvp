using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Tests.Domain;

/// <summary>
/// Unit tests for the heatmap-related domain classes: DemandPing, HeatmapTile, WsConnection.
/// Validates construction, serialization, parsing, and PII exclusion.
/// Requirements: 4.1, 4.6
/// </summary>
public class HeatmapDomainTests
{
    // ─── DemandPing Tests ─────────────────────────────────────────────────

    [Fact]
    public void DemandPing_Constructor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var commuterId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(5);

        var ping = new DemandPing(
            id: id,
            createdAt: now,
            updatedAt: now,
            commuterId: commuterId,
            latitude: 14.5995,
            longitude: 120.9842,
            geohash5: "wdw5e",
            geohash7: "wdw5e0q",
            vehicleType: VehicleType.Jeepney,
            expiresAt: expiresAt);

        Assert.Equal(id, ping.Id);
        Assert.Equal(commuterId, ping.CommuterId);
        Assert.Equal(14.5995, ping.Latitude);
        Assert.Equal(120.9842, ping.Longitude);
        Assert.Equal("wdw5e", ping.Geohash5);
        Assert.Equal("wdw5e0q", ping.Geohash7);
        Assert.Equal(VehicleType.Jeepney, ping.VehicleType);
        Assert.Equal(expiresAt, ping.ExpiresAt);
    }

    [Fact]
    public void DemandPing_Serialize_ContainsAllFields()
    {
        var ping = CreateDemandPing();
        var dict = ping.Serialize();

        Assert.NotNull(dict["id"]);
        Assert.NotNull(dict["commuterId"]);
        Assert.NotNull(dict["latitude"]);
        Assert.NotNull(dict["longitude"]);
        Assert.NotNull(dict["geohash5"]);
        Assert.NotNull(dict["geohash7"]);
        Assert.NotNull(dict["vehicleType"]);
        Assert.NotNull(dict["expiresAt"]);
        Assert.NotNull(dict["createdAt"]);
        Assert.NotNull(dict["updatedAt"]);
    }

    [Fact]
    public void DemandPing_Serialize_Parse_RoundTrip()
    {
        var original = CreateDemandPing();
        var dict = original.Serialize();
        var parsed = DemandPing.Parse(dict);

        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.CommuterId, parsed.CommuterId);
        Assert.Equal(original.Latitude, parsed.Latitude);
        Assert.Equal(original.Longitude, parsed.Longitude);
        Assert.Equal(original.Geohash5, parsed.Geohash5);
        Assert.Equal(original.Geohash7, parsed.Geohash7);
        Assert.Equal(original.VehicleType, parsed.VehicleType);
    }

    [Fact]
    public void DemandPing_Parse_MissingId_Throws()
    {
        var dict = new Dictionary<string, object?> { ["commuterId"] = Guid.NewGuid().ToString() };
        Assert.Throws<ArgumentException>(() => DemandPing.Parse(dict));
    }

    [Fact]
    public void DemandPing_DefaultConstructor_SetsDefaults()
    {
        var ping = new DemandPing();
        Assert.NotEqual(Guid.Empty, ping.Id);
        Assert.Equal(string.Empty, ping.Geohash5);
        Assert.Equal(string.Empty, ping.Geohash7);
    }

    // ─── HeatmapTile Tests ────────────────────────────────────────────────

    [Fact]
    public void HeatmapTile_Constructor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var tile = new HeatmapTile(
            id: id,
            createdAt: now,
            updatedAt: now,
            geohash7: "wdw5e0q",
            demandCount: 5,
            vehicleType: VehicleType.Bus);

        Assert.Equal(id, tile.Id);
        Assert.Equal("wdw5e0q", tile.Geohash7);
        Assert.Equal(5, tile.DemandCount);
        Assert.Equal(VehicleType.Bus, tile.VehicleType);
    }

    [Fact]
    public void HeatmapTile_Serialize_ContainsNoPII()
    {
        var tile = new HeatmapTile(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            geohash7: "wdw5e0q",
            demandCount: 3,
            vehicleType: VehicleType.Jeepney);

        var dict = tile.Serialize();

        // Must contain only geohash7, demandCount, vehicleType + base fields (id, createdAt, updatedAt)
        Assert.True(dict.ContainsKey("geohash7"));
        Assert.True(dict.ContainsKey("demandCount"));
        Assert.True(dict.ContainsKey("vehicleType"));

        // Must NOT contain any PII fields (Req 4.6)
        Assert.False(dict.ContainsKey("commuterId"));
        Assert.False(dict.ContainsKey("name"));
        Assert.False(dict.ContainsKey("email"));
        Assert.False(dict.ContainsKey("deviceId"));
        Assert.False(dict.ContainsKey("userId"));
    }

    [Fact]
    public void HeatmapTile_Serialize_Parse_RoundTrip()
    {
        var original = new HeatmapTile(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            geohash7: "wdw5e0q",
            demandCount: 7,
            vehicleType: VehicleType.UV_Express);

        var dict = original.Serialize();
        var parsed = HeatmapTile.Parse(dict);

        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.Geohash7, parsed.Geohash7);
        Assert.Equal(original.DemandCount, parsed.DemandCount);
        Assert.Equal(original.VehicleType, parsed.VehicleType);
    }

    [Fact]
    public void HeatmapTile_Parse_MissingGeohash7_ReturnsEmpty()
    {
        var dict = new Dictionary<string, object?>
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["createdAt"] = DateTime.UtcNow.ToString("o"),
            ["updatedAt"] = DateTime.UtcNow.ToString("o"),
            ["geohash7"] = null,
            ["demandCount"] = "3",
            ["vehicleType"] = "Jeepney"
        };

        var parsed = HeatmapTile.Parse(dict);
        Assert.Equal(string.Empty, parsed.Geohash7);
    }

    // ─── WsConnection Tests ───────────────────────────────────────────────

    [Fact]
    public void WsConnection_Constructor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var expiresAt = now.AddHours(24);

        var conn = new WsConnection(
            id: id,
            createdAt: now,
            updatedAt: now,
            userId: userId,
            role: UserRole.Driver,
            connectionId: "conn-abc-123",
            connectedAt: now,
            subscribedBbox: "14.5,120.9,14.7,121.1",
            expiresAt: expiresAt);

        Assert.Equal(id, conn.Id);
        Assert.Equal(userId, conn.UserId);
        Assert.Equal(UserRole.Driver, conn.Role);
        Assert.Equal("conn-abc-123", conn.ConnectionId);
        Assert.Equal("14.5,120.9,14.7,121.1", conn.SubscribedBbox);
        Assert.Equal(expiresAt, conn.ExpiresAt);
    }

    [Fact]
    public void WsConnection_Serialize_ContainsAllFields()
    {
        var conn = CreateWsConnection();
        var dict = conn.Serialize();

        Assert.NotNull(dict["userId"]);
        Assert.NotNull(dict["role"]);
        Assert.NotNull(dict["connectionId"]);
        Assert.NotNull(dict["connectedAt"]);
        Assert.NotNull(dict["expiresAt"]);
    }

    [Fact]
    public void WsConnection_Serialize_Parse_RoundTrip()
    {
        var original = CreateWsConnection();
        var dict = original.Serialize();
        var parsed = WsConnection.Parse(dict);

        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.UserId, parsed.UserId);
        Assert.Equal(original.Role, parsed.Role);
        Assert.Equal(original.ConnectionId, parsed.ConnectionId);
        Assert.Equal(original.SubscribedBbox, parsed.SubscribedBbox);
    }

    [Fact]
    public void WsConnection_NullSubscribedBbox_SerializesCorrectly()
    {
        var conn = new WsConnection(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            userId: Guid.NewGuid(),
            role: UserRole.Commuter,
            connectionId: "conn-no-sub",
            connectedAt: DateTime.UtcNow,
            subscribedBbox: null,
            expiresAt: DateTime.UtcNow.AddHours(24));

        var dict = conn.Serialize();
        Assert.Null(dict["subscribedBbox"]);
    }

    [Fact]
    public void WsConnection_Parse_NullSubscribedBbox_ParsesCorrectly()
    {
        var original = new WsConnection(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            userId: Guid.NewGuid(),
            role: UserRole.Commuter,
            connectionId: "conn-no-sub",
            connectedAt: DateTime.UtcNow,
            subscribedBbox: null,
            expiresAt: DateTime.UtcNow.AddHours(24));

        var dict = original.Serialize();
        var parsed = WsConnection.Parse(dict);

        Assert.Null(parsed.SubscribedBbox);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static DemandPing CreateDemandPing()
    {
        return new DemandPing(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            commuterId: Guid.NewGuid(),
            latitude: 14.5995,
            longitude: 120.9842,
            geohash5: "wdw5e",
            geohash7: "wdw5e0q",
            vehicleType: VehicleType.Jeepney,
            expiresAt: DateTime.UtcNow.AddMinutes(5));
    }

    private static WsConnection CreateWsConnection()
    {
        return new WsConnection(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            userId: Guid.NewGuid(),
            role: UserRole.Driver,
            connectionId: "conn-test-123",
            connectedAt: DateTime.UtcNow,
            subscribedBbox: "14.5,120.9,14.7,121.1",
            expiresAt: DateTime.UtcNow.AddHours(24));
    }
}
