using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Fare;
using DomainFareCalculator = BiyaHero.Api.Domain.FareCalculator;
using DomainFareResult = BiyaHero.Api.Domain.FareResult;

namespace BiyaHero.Api.Tests.Domain;

/// <summary>
/// Unit tests for the Domain.FareCalculator class.
/// Validates: Requirements 2.2, 2.3, 2.4, 2.5, 2.10
/// </summary>
public class FareCalculatorDomainTests
{
    private static readonly FareMatrix JeepneyMatrix = new(
        version: "v1",
        vehicleType: "jeepney",
        minFareCentavos: 1300,
        minFareKm: 4.0,
        perKmCentavos: 180,
        discountPercentByCategory: new Dictionary<string, int>
        {
            ["regular"] = 0,
            ["student"] = 20,
            ["senior"] = 20,
            ["pwd"] = 20
        });

    private static readonly FareMatrix BusMatrix = new(
        version: "v1",
        vehicleType: "bus",
        minFareCentavos: 1500,
        minFareKm: 5.0,
        perKmCentavos: 265,
        discountPercentByCategory: new Dictionary<string, int>
        {
            ["regular"] = 0,
            ["student"] = 20,
            ["senior"] = 20,
            ["pwd"] = 20
        });

    private static readonly FareMatrix UvExpressMatrix = new(
        version: "v1",
        vehicleType: "uv_express",
        minFareCentavos: 3000,
        minFareKm: 5.0,
        perKmCentavos: 200,
        discountPercentByCategory: new Dictionary<string, int>
        {
            ["regular"] = 0,
            ["student"] = 20,
            ["senior"] = 20,
            ["pwd"] = 20
        });

    // ─── Constructor Tests ───────────────────────────────────────────────

    [Fact]
    public void Constructor_NullMatrix_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DomainFareCalculator(null!));
    }

    [Fact]
    public void Constructor_ValidMatrix_SetsVehicleTypeAndVersion()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        Assert.Equal("jeepney", calc.VehicleType);
        Assert.Equal("v1", calc.MatrixVersion);
    }

    // ─── Haversine Distance Tests (Req 2.2) ──────────────────────────────

    [Fact]
    public void HaversineDistanceKm_SamePoint_ReturnsZero()
    {
        double distance = DomainFareCalculator.HaversineDistanceKm(14.5995, 120.9842, 14.5995, 120.9842);
        Assert.Equal(0.0, distance, precision: 6);
    }

    [Fact]
    public void HaversineDistanceKm_KnownDistance_Manila_To_Quezon()
    {
        // Manila City Hall to Quezon City Hall: ~8.3 km
        double distance = DomainFareCalculator.HaversineDistanceKm(14.5995, 120.9842, 14.6488, 121.0509);
        Assert.InRange(distance, 8.0, 9.0);
    }

    [Fact]
    public void HaversineDistanceKm_IsSymmetric()
    {
        double d1 = DomainFareCalculator.HaversineDistanceKm(14.5995, 120.9842, 14.6488, 121.0509);
        double d2 = DomainFareCalculator.HaversineDistanceKm(14.6488, 121.0509, 14.5995, 120.9842);
        Assert.Equal(d1, d2, precision: 10);
    }

    // ─── Minimum Fare Tests (Req 2.3) ────────────────────────────────────

    [Fact]
    public void CalculateFromDistance_BelowThreshold_ReturnsMinFare()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(2.0, "regular");

        Assert.Equal(1300, result.BaseFareCentavos);
        Assert.Equal(0, result.DistanceChargeCentavos);
        Assert.Equal(1300, result.TotalFareCentavos);
        Assert.Equal(13.00m, result.TotalFarePhp);
    }

    [Fact]
    public void CalculateFromDistance_ExactlyAtThreshold_ReturnsMinFare()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(4.0, "regular");

        Assert.Equal(1300, result.BaseFareCentavos);
        Assert.Equal(0, result.DistanceChargeCentavos);
        Assert.Equal(1300, result.TotalFareCentavos);
    }

    [Fact]
    public void CalculateFromDistance_ZeroDistance_ReturnsMinFare()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(0.0, "regular");

        Assert.Equal(1300, result.TotalFareCentavos);
    }

    // ─── Per-km Increment Tests (Req 2.4) ────────────────────────────────

    [Fact]
    public void CalculateFromDistance_AboveThreshold_AppliesPerKmIncrement()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        // 5 km: base 1300 + (5 - 4) * 180 = 1300 + 180 = 1480
        var result = calc.CalculateFromDistance(5.0, "regular");

        Assert.Equal(1300, result.BaseFareCentavos);
        Assert.Equal(180, result.DistanceChargeCentavos);
        // 1480 → nearest 25 = 1475
        Assert.Equal(1475, result.TotalFareCentavos);
    }

    [Fact]
    public void CalculateFromDistance_10km_CorrectCalculation()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        // 10 km: base 1300 + (10 - 4) * 180 = 1300 + 1080 = 2380
        var result = calc.CalculateFromDistance(10.0, "regular");

        Assert.Equal(1300, result.BaseFareCentavos);
        Assert.Equal(1080, result.DistanceChargeCentavos);
        // 2380 → nearest 25 = 2375
        Assert.Equal(2375, result.TotalFareCentavos);
    }

    // ─── Discount Tests (Req 2.5) ────────────────────────────────────────

    [Fact]
    public void CalculateFromDistance_StudentDiscount_20PercentOff()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        // Min fare 1300 * 0.80 = 1040 → nearest 25 = 1050
        var result = calc.CalculateFromDistance(2.0, "student");

        Assert.Equal(20, result.DiscountPercent);
        Assert.True(result.DiscountCentavos > 0);
        Assert.Equal(1050, result.TotalFareCentavos);
        Assert.Equal(10.50m, result.TotalFarePhp);
    }

    [Fact]
    public void CalculateFromDistance_SeniorDiscount_20PercentOff()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(2.0, "senior");

        Assert.Equal(20, result.DiscountPercent);
        Assert.Equal(1050, result.TotalFareCentavos);
    }

    [Fact]
    public void CalculateFromDistance_PwdDiscount_20PercentOff()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(2.0, "pwd");

        Assert.Equal(20, result.DiscountPercent);
        Assert.Equal(1050, result.TotalFareCentavos);
    }

    [Fact]
    public void CalculateFromDistance_RegularDiscount_NoReduction()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(2.0, "regular");

        Assert.Equal(0, result.DiscountPercent);
        Assert.Equal(0, result.DiscountCentavos);
        Assert.Equal(1300, result.TotalFareCentavos);
    }

    [Fact]
    public void CalculateFromDistance_NullPassengerType_TreatedAsRegular()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(2.0, null);

        Assert.Equal("regular", result.PassengerType);
        Assert.Equal(0, result.DiscountPercent);
        Assert.Equal(1300, result.TotalFareCentavos);
    }

    [Fact]
    public void CalculateFromDistance_EmptyPassengerType_TreatedAsRegular()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(2.0, "");

        Assert.Equal("regular", result.PassengerType);
        Assert.Equal(1300, result.TotalFareCentavos);
    }

    [Fact]
    public void CalculateFromDistance_CaseInsensitivePassengerType()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(2.0, "STUDENT");

        Assert.Equal("student", result.PassengerType);
        Assert.Equal(20, result.DiscountPercent);
        Assert.Equal(1050, result.TotalFareCentavos);
    }

    [Fact]
    public void CalculateFromDistance_UnknownCategory_NoDiscount()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(2.0, "unknown");

        Assert.Equal(0, result.DiscountPercent);
        Assert.Equal(0, result.DiscountCentavos);
        Assert.Equal(1300, result.TotalFareCentavos);
    }

    // ─── 25-Centavo Rounding Tests (Req 2.4) ─────────────────────────────

    [Theory]
    [InlineData(1300, 1300)]  // Already on boundary
    [InlineData(1312, 1300)]  // Below midpoint → round down
    [InlineData(1313, 1325)]  // Above midpoint → round up
    [InlineData(1325, 1325)]  // Already on boundary
    [InlineData(1337, 1325)]  // Below midpoint → round down
    [InlineData(1338, 1350)]  // Above midpoint → round up
    [InlineData(1040, 1050)]  // 1040 → nearest 25 is 1050
    [InlineData(0, 0)]        // Zero
    public void RoundToNearest25Centavos_CorrectRounding(double input, int expected)
    {
        int result = DomainFareCalculator.RoundToNearest25Centavos(input);
        Assert.Equal(expected, result);
    }

    // ─── Determinism Tests (Req 2.10) ────────────────────────────────────

    [Fact]
    public void Calculate_SameInputs_ProducesSameOutput()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result1 = calc.Calculate(14.5995, 120.9842, 14.6488, 121.0509, "student");
        var result2 = calc.Calculate(14.5995, 120.9842, 14.6488, 121.0509, "student");

        Assert.Equal(result1.TotalFareCentavos, result2.TotalFareCentavos);
        Assert.Equal(result1.DistanceKm, result2.DistanceKm);
        Assert.Equal(result1.MatrixVersion, result2.MatrixVersion);
        Assert.Equal(result1.BaseFareCentavos, result2.BaseFareCentavos);
        Assert.Equal(result1.DistanceChargeCentavos, result2.DistanceChargeCentavos);
        Assert.Equal(result1.DiscountCentavos, result2.DiscountCentavos);
    }

    [Fact]
    public void Calculate_RepeatedCalls_AlwaysDeterministic()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var results = Enumerable.Range(0, 50)
            .Select(_ => calc.Calculate(14.5995, 120.9842, 14.6488, 121.0509, "senior"))
            .ToList();

        var first = results[0];
        Assert.All(results, r =>
        {
            Assert.Equal(first.TotalFareCentavos, r.TotalFareCentavos);
            Assert.Equal(first.DistanceKm, r.DistanceKm);
            Assert.Equal(first.BaseFareCentavos, r.BaseFareCentavos);
            Assert.Equal(first.DiscountCentavos, r.DiscountCentavos);
        });
    }

    // ─── Full Calculate (with coordinates) Tests ─────────────────────────

    [Fact]
    public void Calculate_ShortDistance_ReturnsMinFare()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        // Two very close points (well under 4 km)
        var result = calc.Calculate(14.5995, 120.9842, 14.6010, 120.9850);

        Assert.Equal(1300, result.TotalFareCentavos);
        Assert.Equal(13.00m, result.TotalFarePhp);
        Assert.Equal("v1", result.MatrixVersion);
        Assert.Equal("jeepney", result.VehicleType);
    }

    [Fact]
    public void Calculate_LongerDistance_WithDiscount_CorrectResult()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        // Manila to QC: ~8.3 km
        var result = calc.Calculate(14.5995, 120.9842, 14.6488, 121.0509, "student");

        Assert.InRange(result.DistanceKm, 8.0, 9.0);
        Assert.True(result.TotalFarePhp > 10m);
        Assert.True(result.TotalFarePhp < 20m);
        Assert.Equal("v1", result.MatrixVersion);
        Assert.Equal("student", result.PassengerType);
        Assert.Equal(20, result.DiscountPercent);
        // Verify it's a multiple of 25 centavos
        Assert.Equal(0, result.TotalFareCentavos % 25);
    }

    // ─── FareResult Breakdown Tests ──────────────────────────────────────

    [Fact]
    public void CalculateFromDistance_ReturnsBreakdown()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(6.0, "student");

        Assert.NotNull(result.Breakdown);
        Assert.NotEmpty(result.Breakdown.BaseFareDescription);
        Assert.NotEmpty(result.Breakdown.DistanceChargeDescription);
        Assert.NotEmpty(result.Breakdown.SubtotalDescription);
        Assert.NotEmpty(result.Breakdown.DiscountDescription);
        Assert.NotEmpty(result.Breakdown.RoundingDescription);
    }

    [Fact]
    public void CalculateFromDistance_NoDiscount_EmptyDiscountDescription()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(6.0, "regular");

        Assert.Equal(string.Empty, result.Breakdown.DiscountDescription);
    }

    [Fact]
    public void CalculateFromDistance_BelowThreshold_EmptyDistanceChargeDescription()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);

        var result = calc.CalculateFromDistance(2.0, "regular");

        Assert.Equal(string.Empty, result.Breakdown.DistanceChargeDescription);
    }

    // ─── Multiple Vehicle Types ──────────────────────────────────────────

    [Fact]
    public void CalculateFromDistance_BusMatrix_CorrectFare()
    {
        var calc = new DomainFareCalculator(BusMatrix);

        // 5 km (at threshold): min fare only
        var result = calc.CalculateFromDistance(5.0, "regular");

        Assert.Equal(1500, result.BaseFareCentavos);
        Assert.Equal(0, result.DistanceChargeCentavos);
        Assert.Equal(1500, result.TotalFareCentavos);
        Assert.Equal("bus", result.VehicleType);
    }

    [Fact]
    public void CalculateFromDistance_UvExpressMatrix_CorrectFare()
    {
        var calc = new DomainFareCalculator(UvExpressMatrix);

        // 7 km: base 3000 + (7 - 5) * 200 = 3000 + 400 = 3400
        var result = calc.CalculateFromDistance(7.0, "regular");

        Assert.Equal(3000, result.BaseFareCentavos);
        Assert.Equal(400, result.DistanceChargeCentavos);
        Assert.Equal(3400, result.TotalFareCentavos);
        Assert.Equal("uv_express", result.VehicleType);
    }

    // ─── TotalFare Always Multiple of 25 Centavos ────────────────────────

    [Fact]
    public void Calculate_AllDiscountCategories_FareAlwaysMultipleOf25()
    {
        var calc = new DomainFareCalculator(JeepneyMatrix);
        var categories = new[] { "regular", "student", "senior", "pwd" };
        var distances = new[] { 1.0, 3.5, 4.0, 5.5, 8.3, 12.0, 20.0 };

        foreach (var distance in distances)
        {
            foreach (var category in categories)
            {
                var result = calc.CalculateFromDistance(distance, category);
                Assert.True(result.TotalFareCentavos % 25 == 0,
                    $"Fare {result.TotalFareCentavos} is not a multiple of 25 for distance={distance}, category={category}");
            }
        }
    }
}
