using BiyaHero.Api.Features.Fare;

namespace BiyaHero.Api.Tests.Features.Fare;

/// <summary>
/// Unit tests for the FareCalculator domain class.
/// Validates: Requirements 2.2, 2.3, 2.4, 2.5, 2.10
/// </summary>
public class FareCalculatorTests
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

    // ─── Haversine Distance Tests (Req 2.2) ──────────────────────────────

    [Fact]
    public void HaversineDistanceKm_SamePoint_ReturnsZero()
    {
        double distance = FareCalculator.HaversineDistanceKm(14.5995, 120.9842, 14.5995, 120.9842);
        Assert.Equal(0.0, distance, precision: 6);
    }

    [Fact]
    public void HaversineDistanceKm_KnownDistance_Manila_To_Quezon()
    {
        // Manila City Hall (14.5995, 120.9842) to Quezon City Hall (14.6488, 121.0509)
        // Expected ~8.3 km (approximate)
        double distance = FareCalculator.HaversineDistanceKm(14.5995, 120.9842, 14.6488, 121.0509);
        Assert.InRange(distance, 8.0, 9.0);
    }

    [Fact]
    public void HaversineDistanceKm_ShortDistance_WithinMetroManila()
    {
        // Two points ~1 km apart in Manila
        // Rizal Park (14.5833, 120.9786) to Intramuros (14.5896, 120.9747)
        double distance = FareCalculator.HaversineDistanceKm(14.5833, 120.9786, 14.5896, 120.9747);
        Assert.InRange(distance, 0.5, 1.5);
    }

    [Fact]
    public void HaversineDistanceKm_IsSymmetric()
    {
        double d1 = FareCalculator.HaversineDistanceKm(14.5995, 120.9842, 14.6488, 121.0509);
        double d2 = FareCalculator.HaversineDistanceKm(14.6488, 121.0509, 14.5995, 120.9842);
        Assert.Equal(d1, d2, precision: 10);
    }

    // ─── Minimum Fare Tests (Req 2.3) ────────────────────────────────────

    [Fact]
    public void Calculate_DistanceBelowThreshold_ReturnsMinFare()
    {
        // Two points very close together (well under 4 km)
        var result = FareCalculator.Calculate(
            14.5995, 120.9842,
            14.6010, 120.9850,
            JeepneyMatrix);

        Assert.Equal(13.00m, result.AmountPhp);
        Assert.Equal("v1", result.MatrixVersion);
    }

    [Fact]
    public void Calculate_DistanceExactlyAtThreshold_ReturnsMinFare()
    {
        // Use ComputeRawFareCentavos directly to test the boundary
        int fare = FareCalculator.ComputeRawFareCentavos(4.0, JeepneyMatrix);
        Assert.Equal(1300, fare);
    }

    // ─── Per-km Increment Tests (Req 2.4) ────────────────────────────────

    [Fact]
    public void ComputeRawFareCentavos_AboveThreshold_AppliesPerKmIncrement()
    {
        // 5 km: min_fare + (5 - 4) * 180 = 1300 + 180 = 1480
        int fare = FareCalculator.ComputeRawFareCentavos(5.0, JeepneyMatrix);
        Assert.Equal(1480, fare);
    }

    [Fact]
    public void ComputeRawFareCentavos_10km_CorrectCalculation()
    {
        // 10 km: min_fare + (10 - 4) * 180 = 1300 + 1080 = 2380
        int fare = FareCalculator.ComputeRawFareCentavos(10.0, JeepneyMatrix);
        Assert.Equal(2380, fare);
    }

    // ─── Discount Tests (Req 2.5) ────────────────────────────────────────

    [Fact]
    public void Calculate_RegularDiscount_NoReduction()
    {
        // Use a distance that gives exactly min fare (under threshold)
        var result = FareCalculator.Calculate(
            14.5995, 120.9842,
            14.6010, 120.9850,
            JeepneyMatrix,
            "regular");

        Assert.Equal(13.00m, result.AmountPhp);
    }

    [Fact]
    public void Calculate_StudentDiscount_20PercentOff()
    {
        // Min fare 1300 centavos * 0.80 = 1040 centavos → round to nearest 25 = 1050 → ₱10.50
        var result = FareCalculator.Calculate(
            14.5995, 120.9842,
            14.6010, 120.9850,
            JeepneyMatrix,
            "student");

        Assert.Equal(10.50m, result.AmountPhp);
    }

    [Fact]
    public void Calculate_SeniorDiscount_20PercentOff()
    {
        var result = FareCalculator.Calculate(
            14.5995, 120.9842,
            14.6010, 120.9850,
            JeepneyMatrix,
            "senior");

        Assert.Equal(10.50m, result.AmountPhp);
    }

    [Fact]
    public void Calculate_PwdDiscount_20PercentOff()
    {
        var result = FareCalculator.Calculate(
            14.5995, 120.9842,
            14.6010, 120.9850,
            JeepneyMatrix,
            "pwd");

        Assert.Equal(10.50m, result.AmountPhp);
    }

    [Fact]
    public void Calculate_NullDiscount_TreatedAsRegular()
    {
        var result = FareCalculator.Calculate(
            14.5995, 120.9842,
            14.6010, 120.9850,
            JeepneyMatrix,
            null);

        Assert.Equal(13.00m, result.AmountPhp);
    }

    [Fact]
    public void Calculate_EmptyDiscount_TreatedAsRegular()
    {
        var result = FareCalculator.Calculate(
            14.5995, 120.9842,
            14.6010, 120.9850,
            JeepneyMatrix,
            "");

        Assert.Equal(13.00m, result.AmountPhp);
    }

    [Fact]
    public void Calculate_DiscountCategoryIsCaseInsensitive()
    {
        var result = FareCalculator.Calculate(
            14.5995, 120.9842,
            14.6010, 120.9850,
            JeepneyMatrix,
            "STUDENT");

        Assert.Equal(10.50m, result.AmountPhp);
    }

    // ─── 25-Centavo Rounding Tests (Req 2.4) ─────────────────────────────

    [Theory]
    [InlineData(1300, 1300)]  // Already on boundary
    [InlineData(1312, 1300)]  // Below midpoint → round down
    [InlineData(1313, 1325)]  // Above midpoint → round up
    [InlineData(1325, 1325)]  // Already on boundary
    [InlineData(1337, 1325)]  // Below midpoint → round down
    [InlineData(1338, 1350)]  // Above midpoint → round up
    [InlineData(1350, 1350)]  // Already on boundary
    [InlineData(1040, 1050)]  // 1040 → nearest 25 is 1050 (midpoint rounds up)
    [InlineData(1050, 1050)]  // Already on boundary
    [InlineData(100, 100)]    // Already on boundary (₱1.00)
    [InlineData(0, 0)]        // Zero
    public void RoundToNearest25Centavos_CorrectRounding(double input, int expected)
    {
        int result = FareCalculator.RoundToNearest25Centavos(input);
        Assert.Equal(expected, result);
    }

    // ─── Determinism Tests (Req 2.10) ────────────────────────────────────

    [Fact]
    public void Calculate_SameInputs_ProducesSameOutput()
    {
        var result1 = FareCalculator.Calculate(14.5995, 120.9842, 14.6488, 121.0509, JeepneyMatrix, "student");
        var result2 = FareCalculator.Calculate(14.5995, 120.9842, 14.6488, 121.0509, JeepneyMatrix, "student");

        Assert.Equal(result1.AmountPhp, result2.AmountPhp);
        Assert.Equal(result1.DistanceKm, result2.DistanceKm);
        Assert.Equal(result1.MatrixVersion, result2.MatrixVersion);
    }

    [Fact]
    public void Calculate_RepeatedCalls_AlwaysDeterministic()
    {
        var results = Enumerable.Range(0, 100)
            .Select(_ => FareCalculator.Calculate(14.5995, 120.9842, 14.6488, 121.0509, JeepneyMatrix, "senior"))
            .ToList();

        var first = results[0];
        Assert.All(results, r =>
        {
            Assert.Equal(first.AmountPhp, r.AmountPhp);
            Assert.Equal(first.DistanceKm, r.DistanceKm);
            Assert.Equal(first.MatrixVersion, r.MatrixVersion);
        });
    }

    // ─── Integration-style Tests ─────────────────────────────────────────

    [Fact]
    public void Calculate_LongerDistance_WithDiscount_CorrectResult()
    {
        // Use a known distance: ~8.3 km from Manila to QC
        // Raw: 1300 + (8.3 - 4) * 180 = 1300 + 774 = 2074 centavos
        // With 20% discount: 2074 * 0.80 = 1659.2 centavos
        // Round to nearest 25: 1650 centavos = ₱16.50
        var result = FareCalculator.Calculate(
            14.5995, 120.9842,
            14.6488, 121.0509,
            JeepneyMatrix,
            "student");

        // Distance should be ~8.3 km
        Assert.InRange(result.DistanceKm, 8.0, 9.0);
        // Fare should be reasonable for this distance with discount
        Assert.True(result.AmountPhp > 10m);
        Assert.True(result.AmountPhp < 20m);
        Assert.Equal("v1", result.MatrixVersion);
        // Verify it's a multiple of 0.25
        Assert.Equal(0m, result.AmountPhp % 0.25m);
    }

    [Fact]
    public void Calculate_ResultFare_AlwaysMultipleOf25Centavos()
    {
        // Test various distances to ensure rounding always produces multiples of 25 centavos
        var testCases = new[]
        {
            (14.5995, 120.9842, 14.6010, 120.9850),  // Very short
            (14.5995, 120.9842, 14.6200, 121.0100),  // Medium
            (14.5995, 120.9842, 14.6488, 121.0509),  // Longer
        };

        foreach (var (lat1, lng1, lat2, lng2) in testCases)
        {
            foreach (var discount in new[] { "regular", "student", "senior", "pwd" })
            {
                var result = FareCalculator.Calculate(lat1, lng1, lat2, lng2, JeepneyMatrix, discount);
                Assert.True(result.AmountPhp % 0.25m == 0m,
                    $"Fare {result.AmountPhp} is not a multiple of ₱0.25 for discount={discount}");
            }
        }
    }

    [Fact]
    public void Calculate_ReturnsDistanceInKm()
    {
        var result = FareCalculator.Calculate(14.5995, 120.9842, 14.6488, 121.0509, JeepneyMatrix);
        Assert.True(result.DistanceKm > 0);
    }

    [Fact]
    public void Calculate_ReturnsMatrixVersion()
    {
        var result = FareCalculator.Calculate(14.5995, 120.9842, 14.6488, 121.0509, JeepneyMatrix);
        Assert.Equal("v1", result.MatrixVersion);
    }
}
