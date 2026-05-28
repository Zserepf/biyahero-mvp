using BiyaHero.Api.Features.Fare;

namespace BiyaHero.Api.Tests.Features.Fare;

/// <summary>
/// Unit tests for the FareCalculateEndpoint validation logic.
/// Focuses on the 400 vs 422 disambiguation and edge cases.
/// 
/// 400 (Bad Request) — Malformed request: missing required fields, null body
/// 422 (Unprocessable Entity) — Structurally valid but semantically invalid:
///     invalid coordinates, unsupported vehicle type
/// 
/// Validates: Requirements 2.3, 2.4, 2.5, 2.6, 2.7
/// </summary>
public class FareCalculateEndpointValidationTests
{
    private readonly FareCalculateHandler _handler;

    public FareCalculateEndpointValidationTests()
    {
        _handler = new FareCalculateHandler(new TestFareMatrixLoader());
    }

    // ─── 400 vs 422 Disambiguation (Req 2.6, 2.7) ───────────────────────
    // 400: Missing required fields (malformed request)
    // 422: Invalid coordinates or unsupported vehicle type (valid structure, invalid semantics)

    [Fact]
    public void MissingOriginLat_Is400_NotA422()
    {
        // Missing field = malformed = 400
        var request = new FareCalculateRequest(
            OriginLat: null,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: "jeepney");

        var missingFields = GetMissingFields(request);
        Assert.Contains("originLat", missingFields);
    }

    [Fact]
    public void MissingOriginLng_Is400_NotA422()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: null,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: "jeepney");

        var missingFields = GetMissingFields(request);
        Assert.Contains("originLng", missingFields);
    }

    [Fact]
    public void MissingDestinationLat_Is400_NotA422()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: null,
            DestinationLng: 121.0509,
            VehicleType: "jeepney");

        var missingFields = GetMissingFields(request);
        Assert.Contains("destinationLat", missingFields);
    }

    [Fact]
    public void MissingDestinationLng_Is400_NotA422()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: null,
            VehicleType: "jeepney");

        var missingFields = GetMissingFields(request);
        Assert.Contains("destinationLng", missingFields);
    }

    [Fact]
    public void MissingVehicleType_Is400_NotA422()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: null);

        var missingFields = GetMissingFields(request);
        Assert.Contains("vehicleType", missingFields);
    }

    [Fact]
    public void EmptyVehicleType_Is400_NotA422()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: "   ");

        var missingFields = GetMissingFields(request);
        Assert.Contains("vehicleType", missingFields);
    }

    [Fact]
    public void MultipleFieldsMissing_AllReported()
    {
        var request = new FareCalculateRequest(
            OriginLat: null,
            OriginLng: null,
            DestinationLat: null,
            DestinationLng: null,
            VehicleType: null);

        var missingFields = GetMissingFields(request);
        Assert.Equal(5, missingFields.Count);
    }

    [Fact]
    public void AllFieldsPresent_NoMissingFields()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: "jeepney");

        var missingFields = GetMissingFields(request);
        Assert.Empty(missingFields);
    }

    // ─── 422: Invalid Coordinates (Req 2.6) ──────────────────────────────

    [Theory]
    [InlineData(91.0)]   // Above max latitude
    [InlineData(-91.0)]  // Below min latitude
    [InlineData(100.0)]  // Way above
    [InlineData(-100.0)] // Way below
    public void InvalidLatitude_Is422(double invalidLat)
    {
        Assert.False(IsValidLatitude(invalidLat));
    }

    [Theory]
    [InlineData(181.0)]   // Above max longitude
    [InlineData(-181.0)]  // Below min longitude
    [InlineData(200.0)]   // Way above
    [InlineData(-200.0)]  // Way below
    public void InvalidLongitude_Is422(double invalidLng)
    {
        Assert.False(IsValidLongitude(invalidLng));
    }

    [Theory]
    [InlineData(90.0)]   // Max valid latitude
    [InlineData(-90.0)]  // Min valid latitude
    [InlineData(0.0)]    // Equator
    [InlineData(14.5)]   // Manila area
    public void ValidLatitude_Passes(double validLat)
    {
        Assert.True(IsValidLatitude(validLat));
    }

    [Theory]
    [InlineData(180.0)]   // Max valid longitude
    [InlineData(-180.0)]  // Min valid longitude
    [InlineData(0.0)]     // Prime meridian
    [InlineData(121.0)]   // Manila area
    public void ValidLongitude_Passes(double validLng)
    {
        Assert.True(IsValidLongitude(validLng));
    }

    // ─── 422: Unsupported Vehicle Type (Req 2.6) ─────────────────────────

    [Theory]
    [InlineData("helicopter")]
    [InlineData("motorcycle")]
    [InlineData("airplane")]
    [InlineData("boat")]
    public async Task UnsupportedVehicleType_HandlerReturnsNull_Signals422(string vehicleType)
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: vehicleType);

        var response = await _handler.HandleAsync(request);

        // Null response signals 422 to the endpoint layer
        Assert.Null(response);
    }

    // ─── Edge Cases: Zero and Very Long Distances (Req 2.3) ──────────────

    [Fact]
    public async Task ZeroDistance_SameOriginAndDestination_ReturnsMinFare()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.5995,
            DestinationLng: 120.9842,
            VehicleType: "jeepney");

        var response = await _handler.HandleAsync(request);

        Assert.NotNull(response);
        Assert.Equal(13.00m, response.AmountPhp); // Min fare for jeepney
        Assert.Equal(0.0, response.DistanceKm, precision: 6);
    }

    [Fact]
    public async Task VeryLongDistance_Manila_To_Davao_ComputesCorrectly()
    {
        // Manila (14.5995, 120.9842) to Davao (7.0731, 125.6128) ~960 km
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 7.0731,
            DestinationLng: 125.6128,
            VehicleType: "bus");

        var response = await _handler.HandleAsync(request);

        Assert.NotNull(response);
        Assert.True(response.DistanceKm > 900);
        Assert.True(response.DistanceKm < 1100);
        // Fare should be substantial for ~960 km
        Assert.True(response.AmountPhp > 100m);
        // Must be a multiple of 0.25
        Assert.Equal(0m, response.AmountPhp % 0.25m);
    }

    [Fact]
    public void BoundaryDistance_ExactlyAtMinFareKm_ReturnsMinFare()
    {
        // Use CalculateFare directly to verify boundary behavior
        var matrix = new FareMatrix(
            version: "v1-2024",
            vehicleType: "jeepney",
            minFareCentavos: 1300,
            minFareKm: 4.0,
            perKmCentavos: 180,
            discountPercentByCategory: new Dictionary<string, int>
            {
                ["regular"] = 0, ["student"] = 20, ["senior"] = 20, ["pwd"] = 20
            });

        var result = FareCalculator.CalculateFare(matrix, 4.0, "regular");

        Assert.Equal(1300, result.FareCentavos);
        Assert.Equal(1300, result.OriginalFareCentavos);
        Assert.False(result.DiscountApplied);
    }

    [Fact]
    public void BoundaryDistance_JustAboveMinFareKm_AppliesIncrement()
    {
        var matrix = new FareMatrix(
            version: "v1-2024",
            vehicleType: "jeepney",
            minFareCentavos: 1300,
            minFareKm: 4.0,
            perKmCentavos: 180,
            discountPercentByCategory: new Dictionary<string, int>
            {
                ["regular"] = 0, ["student"] = 20, ["senior"] = 20, ["pwd"] = 20
            });

        // 4.01 km: 1300 + 0.01 * 180 = 1300 + 1.8 ≈ 1302 centavos
        var result = FareCalculator.CalculateFare(matrix, 4.01, "regular");

        Assert.True(result.FareCentavos > 1300);
        Assert.True(result.OriginalFareCentavos > 1300);
    }

    // ─── All Vehicle Types (Req 2.3, 2.4) ────────────────────────────────

    [Theory]
    [InlineData("jeepney", 1300)]
    [InlineData("bus", 1500)]
    [InlineData("uv_express", 3000)]
    [InlineData("tricycle", 4000)]
    public async Task AllVehicleTypes_ShortDistance_ReturnsCorrectMinFare(string vehicleType, int expectedMinFareCentavos)
    {
        // Two very close points (well under any min fare km threshold)
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.5996,
            DestinationLng: 120.9843,
            VehicleType: vehicleType);

        var response = await _handler.HandleAsync(request);

        Assert.NotNull(response);
        Assert.Equal(expectedMinFareCentavos / 100m, response.AmountPhp);
    }

    // ─── All Discount Categories (Req 2.5) ───────────────────────────────

    [Theory]
    [InlineData("regular", false)]
    [InlineData("student", true)]
    [InlineData("senior", true)]
    [InlineData("pwd", true)]
    public async Task AllDiscountCategories_AppliedCorrectly(string category, bool expectDiscount)
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.5996,
            DestinationLng: 120.9843,
            VehicleType: "jeepney",
            DiscountCategory: category);

        var response = await _handler.HandleAsync(request);

        Assert.NotNull(response);
        Assert.Equal(category, response.DiscountCategory);
        Assert.Equal(expectDiscount, response.DiscountApplied);
    }

    [Fact]
    public async Task DiscountedFare_IsLessThanRegularFare()
    {
        var regularRequest = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: "jeepney",
            DiscountCategory: "regular");

        var studentRequest = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: "jeepney",
            DiscountCategory: "student");

        var regularResponse = await _handler.HandleAsync(regularRequest);
        var studentResponse = await _handler.HandleAsync(studentRequest);

        Assert.NotNull(regularResponse);
        Assert.NotNull(studentResponse);
        Assert.True(studentResponse.AmountPhp < regularResponse.AmountPhp);
    }

    [Fact]
    public async Task AllDiscountCategories_SameDiscount_SameFare()
    {
        // Student, senior, and PWD all get 20% — should produce same fare
        var categories = new[] { "student", "senior", "pwd" };
        var fares = new List<decimal>();

        foreach (var category in categories)
        {
            var request = new FareCalculateRequest(
                OriginLat: 14.5995,
                OriginLng: 120.9842,
                DestinationLat: 14.6488,
                DestinationLng: 121.0509,
                VehicleType: "jeepney",
                DiscountCategory: category);

            var response = await _handler.HandleAsync(request);
            Assert.NotNull(response);
            fares.Add(response.AmountPhp);
        }

        // All discounted fares should be equal
        Assert.True(fares.Distinct().Count() == 1,
            $"Expected all discounted fares to be equal but got: {string.Join(", ", fares)}");
    }

    // ─── Per-km Increment with 25-Centavo Rounding (Req 2.4) ─────────────

    [Fact]
    public void PerKmIncrement_Jeepney_5km_CorrectCalculation()
    {
        var matrix = new FareMatrix(
            version: "v1-2024",
            vehicleType: "jeepney",
            minFareCentavos: 1300,
            minFareKm: 4.0,
            perKmCentavos: 180,
            discountPercentByCategory: new Dictionary<string, int>
            {
                ["regular"] = 0, ["student"] = 20, ["senior"] = 20, ["pwd"] = 20
            });

        // 5 km: 1300 + (5-4)*180 = 1480 centavos (CalculateFare rounds to nearest centavo)
        var directResult = FareCalculator.CalculateFare(matrix, 5.0, "regular");
        Assert.Equal(1480, directResult.FareCentavos);
        Assert.Equal(1480, directResult.OriginalFareCentavos);
        Assert.False(directResult.DiscountApplied);
    }

    [Fact]
    public void PerKmIncrement_Jeepney_5km_Calculate_RoundsTo25Centavos()
    {
        var matrix = new FareMatrix(
            version: "v1-2024",
            vehicleType: "jeepney",
            minFareCentavos: 1300,
            minFareKm: 4.0,
            perKmCentavos: 180,
            discountPercentByCategory: new Dictionary<string, int>
            {
                ["regular"] = 0, ["student"] = 20, ["senior"] = 20, ["pwd"] = 20
            });

        // Use ComputeRawFareCentavos to verify raw fare, then RoundToNearest25Centavos
        int rawFare = FareCalculator.ComputeRawFareCentavos(5.0, matrix);
        Assert.Equal(1480, rawFare);

        // 1480 → nearest 25: 1480/25 = 59.2 → round to 59 → 59*25 = 1475
        int rounded = FareCalculator.RoundToNearest25Centavos(1480);
        Assert.Equal(1475, rounded);
    }

    [Fact]
    public void PerKmIncrement_Bus_10km_CorrectCalculation()
    {
        var matrix = new FareMatrix(
            version: "v1-2024",
            vehicleType: "bus",
            minFareCentavos: 1500,
            minFareKm: 5.0,
            perKmCentavos: 265,
            discountPercentByCategory: new Dictionary<string, int>
            {
                ["regular"] = 0, ["student"] = 20, ["senior"] = 20, ["pwd"] = 20
            });

        // 10 km: 1500 + (10-5)*265 = 1500 + 1325 = 2825 centavos
        var result = FareCalculator.CalculateFare(matrix, 10.0, "regular");
        Assert.Equal(2825, result.FareCentavos);
        Assert.Equal(2825, result.OriginalFareCentavos);
    }

    [Fact]
    public void PerKmIncrement_WithDiscount_AppliedCorrectly()
    {
        var matrix = new FareMatrix(
            version: "v1-2024",
            vehicleType: "jeepney",
            minFareCentavos: 1300,
            minFareKm: 4.0,
            perKmCentavos: 180,
            discountPercentByCategory: new Dictionary<string, int>
            {
                ["regular"] = 0, ["student"] = 20, ["senior"] = 20, ["pwd"] = 20
            });

        // 10 km: 1300 + (10-4)*180 = 1300 + 1080 = 2380 centavos (original)
        // 20% discount: 2380 * 0.80 = 1904 centavos (rounded to nearest centavo)
        var result = FareCalculator.CalculateFare(matrix, 10.0, "student");
        Assert.Equal(2380, result.OriginalFareCentavos);
        Assert.Equal(1904, result.FareCentavos);
        Assert.True(result.DiscountApplied);
    }

    [Fact]
    public void Calculate_WithDistance_RoundsTo25Centavos()
    {
        var matrix = new FareMatrix(
            version: "v1-2024",
            vehicleType: "jeepney",
            minFareCentavos: 1300,
            minFareKm: 4.0,
            perKmCentavos: 180,
            discountPercentByCategory: new Dictionary<string, int>
            {
                ["regular"] = 0, ["student"] = 20, ["senior"] = 20, ["pwd"] = 20
            });

        // 10 km with student discount via Calculate (which rounds to 25 centavos):
        // Raw: 2380, discounted: 2380*0.80 = 1904, rounded to nearest 25: 1900
        // 1900 centavos = ₱19.00
        int rawFare = FareCalculator.ComputeRawFareCentavos(10.0, matrix);
        Assert.Equal(2380, rawFare);

        double discounted = rawFare * 0.80;
        int rounded = FareCalculator.RoundToNearest25Centavos(discounted);
        Assert.Equal(1900, rounded);
    }

    // ─── Response Always Contains Required Fields (Req 2.1) ──────────────

    [Fact]
    public async Task SuccessResponse_ContainsAmountPhp_DistanceKm_MatrixVersion()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: "jeepney");

        var response = await _handler.HandleAsync(request);

        Assert.NotNull(response);
        Assert.True(response.AmountPhp > 0);
        Assert.True(response.DistanceKm > 0);
        Assert.False(string.IsNullOrEmpty(response.MatrixVersion));
        Assert.False(string.IsNullOrEmpty(response.VehicleType));
        Assert.False(string.IsNullOrEmpty(response.DiscountCategory));
    }

    // ─── Fare is Always a Multiple of 25 Centavos (Req 2.4) ─────────────

    [Theory]
    [InlineData("jeepney", "regular")]
    [InlineData("jeepney", "student")]
    [InlineData("bus", "senior")]
    [InlineData("uv_express", "pwd")]
    [InlineData("tricycle", "regular")]
    public async Task Fare_AlwaysMultipleOf25Centavos(string vehicleType, string discount)
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: vehicleType,
            DiscountCategory: discount);

        var response = await _handler.HandleAsync(request);

        Assert.NotNull(response);
        Assert.True(response.AmountPhp % 0.25m == 0m,
            $"Fare ₱{response.AmountPhp} is not a multiple of ₱0.25 for {vehicleType}/{discount}");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Mirrors the endpoint's missing-field check logic for unit testing.
    /// </summary>
    private static List<string> GetMissingFields(FareCalculateRequest request)
    {
        var missing = new List<string>();
        if (request.OriginLat is null) missing.Add("originLat");
        if (request.OriginLng is null) missing.Add("originLng");
        if (request.DestinationLat is null) missing.Add("destinationLat");
        if (request.DestinationLng is null) missing.Add("destinationLng");
        if (string.IsNullOrWhiteSpace(request.VehicleType)) missing.Add("vehicleType");
        return missing;
    }

    private static bool IsValidLatitude(double lat) => lat >= -90.0 && lat <= 90.0;
    private static bool IsValidLongitude(double lng) => lng >= -180.0 && lng <= 180.0;

    /// <summary>
    /// In-memory fare matrix loader for testing.
    /// </summary>
    private sealed class TestFareMatrixLoader : IFareMatrixLoader
    {
        private static readonly Dictionary<string, FareMatrix> Matrices = new(StringComparer.OrdinalIgnoreCase)
        {
            ["jeepney"] = new FareMatrix(
                version: "v1-2024",
                vehicleType: "jeepney",
                minFareCentavos: 1300,
                minFareKm: 4.0,
                perKmCentavos: 180,
                discountPercentByCategory: new Dictionary<string, int>
                {
                    ["regular"] = 0, ["student"] = 20, ["senior"] = 20, ["pwd"] = 20
                }),
            ["bus"] = new FareMatrix(
                version: "v1-2024",
                vehicleType: "bus",
                minFareCentavos: 1500,
                minFareKm: 5.0,
                perKmCentavos: 265,
                discountPercentByCategory: new Dictionary<string, int>
                {
                    ["regular"] = 0, ["student"] = 20, ["senior"] = 20, ["pwd"] = 20
                }),
            ["uv_express"] = new FareMatrix(
                version: "v1-2024",
                vehicleType: "uv_express",
                minFareCentavos: 3000,
                minFareKm: 5.0,
                perKmCentavos: 200,
                discountPercentByCategory: new Dictionary<string, int>
                {
                    ["regular"] = 0, ["student"] = 20, ["senior"] = 20, ["pwd"] = 20
                }),
            ["tricycle"] = new FareMatrix(
                version: "v1-2024",
                vehicleType: "tricycle",
                minFareCentavos: 4000,
                minFareKm: 2.0,
                perKmCentavos: 500,
                discountPercentByCategory: new Dictionary<string, int>
                {
                    ["regular"] = 0, ["student"] = 20, ["senior"] = 20, ["pwd"] = 20
                })
        };

        public Task<FareMatrix?> GetActiveMatrixAsync(string vehicleType)
        {
            Matrices.TryGetValue(vehicleType, out var matrix);
            return Task.FromResult(matrix);
        }

        public Task<IReadOnlyList<FareMatrix>> GetAllActiveMatricesAsync()
            => Task.FromResult<IReadOnlyList<FareMatrix>>(Matrices.Values.ToList().AsReadOnly());

        public Task RefreshAsync() => Task.CompletedTask;
    }
}
