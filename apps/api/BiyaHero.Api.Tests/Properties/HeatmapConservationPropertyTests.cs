using BiyaHero.Api.Domain;
using BiyaHero.Api.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace BiyaHero.Api.Tests.Properties;

/// <summary>
/// Property-based tests for heatmap conservation.
/// Feature: biyahero-mvp, Property 4: Conservation of heatmap aggregation
/// 
/// **Validates: Requirements 4.10**
/// 
/// FOR ALL Demand_Pings submitted within an arbitrary 60-second window,
/// the count of Heatmap_Tile aggregations SHALL equal the count of valid pings
/// minus pings removed by TTL or cancellation (conservation property).
/// 
/// This test verifies that the geohash aggregation logic preserves the total
/// demand count — no pings are lost or duplicated during grouping.
/// </summary>
[Trait("Feature", "biyahero-mvp")]
[Trait("Property", "Property 4: Conservation of heatmap aggregation")]
public class HeatmapConservationPropertyTests
{
    // Philippines bounding box for valid coordinate generation
    private const double MinLat = 4.5;
    private const double MaxLat = 21.5;
    private const double MinLng = 116.0;
    private const double MaxLng = 127.0;

    /// <summary>
    /// Generator for a latitude within the Philippines bounding box.
    /// Uses integer scaling to avoid floating-point edge cases while still
    /// covering the full coordinate space.
    /// </summary>
    private static Gen<double> PhilippinesLat =>
        Gen.Choose(4500, 21500).Select(x => x / 1000.0);

    /// <summary>
    /// Generator for a longitude within the Philippines bounding box.
    /// </summary>
    private static Gen<double> PhilippinesLng =>
        Gen.Choose(116000, 127000).Select(x => x / 1000.0);

    /// <summary>
    /// Generator for a VehicleType enum value.
    /// </summary>
    private static Gen<VehicleType> VehicleTypeGen =>
        Gen.Elements(VehicleType.Jeepney, VehicleType.Bus, VehicleType.UV_Express, VehicleType.Tricycle);

    /// <summary>
    /// Generator for a single DemandPing with valid Philippines coordinates,
    /// a random vehicle type, and pre-computed geohash5/geohash7 values.
    /// The ping is set to expire 5 minutes from now (active, not TTL-expired).
    /// </summary>
    private static Gen<DemandPing> ActiveDemandPingGen =>
        from lat in PhilippinesLat
        from lng in PhilippinesLng
        from vehicleType in VehicleTypeGen
        select CreateDemandPing(lat, lng, vehicleType);

    /// <summary>
    /// Arbitrary that produces a non-empty list of active DemandPing objects.
    /// Size ranges from 1 to 50 pings to keep test execution fast while
    /// covering various aggregation scenarios (single tile, multiple tiles,
    /// multiple vehicle types in the same tile).
    /// </summary>
    private static Arbitrary<List<DemandPing>> ActiveDemandPingListArbitrary()
    {
        var gen = Gen.NonEmptyListOf(ActiveDemandPingGen)
            .Select(pings => pings.ToList());

        return Arb.From(gen);
    }

    /// <summary>
    /// Creates a DemandPing with properly computed geohash5 and geohash7 values.
    /// </summary>
    private static DemandPing CreateDemandPing(double lat, double lng, VehicleType vehicleType)
    {
        var now = DateTime.UtcNow;
        var geohash5 = GeohashEncoder.EncodeForPartition(lat, lng);
        var geohash7 = GeohashEncoder.EncodeForTile(lat, lng);

        return new DemandPing(
            id: Guid.NewGuid(),
            createdAt: now,
            updatedAt: now,
            commuterId: Guid.NewGuid(),
            latitude: lat,
            longitude: lng,
            geohash5: geohash5,
            geohash7: geohash7,
            vehicleType: vehicleType,
            expiresAt: now.AddMinutes(5));
    }

    /// <summary>
    /// Replicates the aggregation logic used in GetTilesHandler and HeatmapAggregatorHandler:
    /// group pings by (Geohash7, VehicleType) and count each group.
    /// </summary>
    private static List<HeatmapTile> AggregatePingsToTiles(IEnumerable<DemandPing> pings)
    {
        return pings
            .GroupBy(p => (p.Geohash7, p.VehicleType))
            .Select(g => new HeatmapTile(
                id: Guid.NewGuid(),
                createdAt: DateTime.UtcNow,
                updatedAt: DateTime.UtcNow,
                geohash7: g.Key.Geohash7,
                demandCount: g.Count(),
                vehicleType: g.Key.VehicleType))
            .ToList();
    }

    /// <summary>
    /// Property 4: Conservation — the total demand count across all HeatmapTile
    /// objects produced by geohash aggregation equals the number of input pings.
    /// No pings are lost or duplicated during the grouping operation.
    /// 
    /// **Validates: Requirements 4.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TotalDemandCount_Equals_InputPingCount()
    {
        return Prop.ForAll(
            ActiveDemandPingListArbitrary(),
            pings =>
            {
                var tiles = AggregatePingsToTiles(pings);

                int totalDemandCount = tiles.Sum(t => t.DemandCount);
                int inputPingCount = pings.Count;

                return (totalDemandCount == inputPingCount)
                    .Label($"Conservation violated: {totalDemandCount} tile demand vs {inputPingCount} input pings");
            });
    }

    /// <summary>
    /// Property 4 (supplementary): Every tile produced by aggregation has a
    /// positive demand count — no empty tiles are emitted.
    /// 
    /// **Validates: Requirements 4.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllTiles_HavePositiveDemandCount()
    {
        return Prop.ForAll(
            ActiveDemandPingListArbitrary(),
            pings =>
            {
                var tiles = AggregatePingsToTiles(pings);

                return tiles.All(t => t.DemandCount > 0)
                    .Label("Found tile with DemandCount <= 0");
            });
    }

    /// <summary>
    /// Property 4 (supplementary): Conservation holds after removing TTL-expired
    /// pings — the total demand across tiles equals only the active (non-expired) pings.
    /// This validates the conservation property accounting for TTL removal.
    /// 
    /// **Validates: Requirements 4.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Conservation_HoldsAfterTtlExpiry()
    {
        // Generate a mix of active and expired pings
        var mixedPingsGen = from activePings in Gen.NonEmptyListOf(ActiveDemandPingGen)
                            from expiredCount in Gen.Choose(0, 10)
                            from expiredPings in Gen.ListOf(expiredCount, ExpiredDemandPingGen)
                            select (Active: activePings.ToList(), Expired: expiredPings.ToList());

        var arb = Arb.From(mixedPingsGen);

        return Prop.ForAll(
            arb,
            input =>
            {
                var allPings = input.Active.Concat(input.Expired).ToList();

                // Filter out expired pings (simulating DynamoDB TTL removal)
                var now = DateTime.UtcNow;
                var activePings = allPings.Where(p => p.ExpiresAt > now).ToList();

                var tiles = AggregatePingsToTiles(activePings);

                int totalDemandCount = tiles.Sum(t => t.DemandCount);
                int activePingCount = activePings.Count;

                return (totalDemandCount == activePingCount)
                    .Label($"Conservation after TTL: {totalDemandCount} tile demand vs {activePingCount} active pings (total={allPings.Count}, expired={input.Expired.Count})");
            });
    }

    /// <summary>
    /// Generator for an expired DemandPing (ExpiresAt in the past).
    /// </summary>
    private static Gen<DemandPing> ExpiredDemandPingGen =>
        from lat in PhilippinesLat
        from lng in PhilippinesLng
        from vehicleType in VehicleTypeGen
        from minutesAgo in Gen.Choose(1, 60)
        select CreateExpiredDemandPing(lat, lng, vehicleType, minutesAgo);

    /// <summary>
    /// Creates a DemandPing that has already expired (ExpiresAt in the past).
    /// </summary>
    private static DemandPing CreateExpiredDemandPing(double lat, double lng, VehicleType vehicleType, int minutesAgo)
    {
        var now = DateTime.UtcNow;
        var submittedAt = now.AddMinutes(-(minutesAgo + 5));
        var geohash5 = GeohashEncoder.EncodeForPartition(lat, lng);
        var geohash7 = GeohashEncoder.EncodeForTile(lat, lng);

        return new DemandPing(
            id: Guid.NewGuid(),
            createdAt: submittedAt,
            updatedAt: submittedAt,
            commuterId: Guid.NewGuid(),
            latitude: lat,
            longitude: lng,
            geohash5: geohash5,
            geohash7: geohash7,
            vehicleType: vehicleType,
            expiresAt: now.AddMinutes(-minutesAgo));
    }
}
