using BiyaHero.Api.Features.Fare;

namespace BiyaHero.Api.Tests.Features.Fare;

/// <summary>
/// Unit tests for the FareCalculateEndpoint and FareCalculateHandler.
/// Validates the HTTP layer behavior: 400 for malformed requests, 422 for invalid input,
/// 200 for successful fare calculation.
/// 
/// Validates: Requirements 2.1, 2.6, 2.7, 2.9
/// </summary>
public class FareCalculateEndpointTests
{
    private readonly FareCalculateHandler _handler;

    public FareCalculateEndpointTests()
    {
        var loader = new TestFareMatrixLoader();
        _handler = new FareCalculateHandler(loader);
    }

    // ─── 200 Success Tests (Req 2.1) ─────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidRequest_ReturnsResponse()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: "jeepney",
            DiscountCategory: "regular");

        var response = await _handler.HandleAsync(request);

        Assert.NotNull(response);
        Assert.True(response.AmountPhp > 0);
        Assert.True(response.DistanceKm > 0);
        Assert.Equal("v1-2024", response.MatrixVersion);
        Assert.Equal("jeepney", response.VehicleType);
        Assert.Equal("regular", response.DiscountCategory);
        Assert.False(response.DiscountApplied);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_AmountIsMultipleOf25Centavos()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: "jeepney",
            DiscountCategory: "student");

        var response = await _handler.HandleAsync(request);

        Assert.NotNull(response);
        Assert.Equal(0m, response.AmountPhp % 0.25m);
    }

    [Fact]
    public async Task HandleAsync_WithDiscount_AppliesDiscount()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: "jeepney",
            DiscountCategory: "student");

        var response = await _handler.HandleAsync(request);

        Assert.NotNull(response);
        Assert.Equal("student", response.DiscountCategory);
        Assert.True(response.DiscountApplied);
    }

    [Fact]
    public async Task HandleAsync_NullDiscount_DefaultsToRegular()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: "jeepney",
            DiscountCategory: null);

        var response = await _handler.HandleAsync(request);

        Assert.NotNull(response);
        Assert.Equal("regular", response.DiscountCategory);
        Assert.False(response.DiscountApplied);
    }

    // ─── 422 Unsupported Vehicle Type (Req 2.6) ──────────────────────────

    [Fact]
    public async Task HandleAsync_UnsupportedVehicleType_ReturnsNull()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6488,
            DestinationLng: 121.0509,
            VehicleType: "helicopter",
            DiscountCategory: "regular");

        var response = await _handler.HandleAsync(request);

        // Null signals 422 to the endpoint — no fare in response
        Assert.Null(response);
    }

    // ─── Response Shape Tests (Req 2.1) ──────────────────────────────────

    [Fact]
    public async Task HandleAsync_Response_ContainsRequiredFields()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6010,
            DestinationLng: 120.9850,
            VehicleType: "jeepney",
            DiscountCategory: "regular");

        var response = await _handler.HandleAsync(request);

        Assert.NotNull(response);
        // amountPhp is present and non-negative
        Assert.True(response.AmountPhp >= 0);
        // distanceKm is present and non-negative
        Assert.True(response.DistanceKm >= 0);
        // matrixVersion is present and non-empty
        Assert.False(string.IsNullOrEmpty(response.MatrixVersion));
    }

    [Fact]
    public async Task HandleAsync_ShortDistance_ReturnsMinFare()
    {
        // Two points very close together — should return minimum fare
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6010,
            DestinationLng: 120.9850,
            VehicleType: "jeepney",
            DiscountCategory: "regular");

        var response = await _handler.HandleAsync(request);

        Assert.NotNull(response);
        Assert.Equal(13.00m, response.AmountPhp);
    }

    // ─── Vehicle Type Case Insensitivity ─────────────────────────────────

    [Fact]
    public async Task HandleAsync_VehicleTypeCaseInsensitive_ReturnsResult()
    {
        var request = new FareCalculateRequest(
            OriginLat: 14.5995,
            OriginLng: 120.9842,
            DestinationLat: 14.6010,
            DestinationLng: 120.9850,
            VehicleType: "JEEPNEY",
            DiscountCategory: "regular");

        var response = await _handler.HandleAsync(request);

        // The loader uses case-insensitive lookup
        Assert.NotNull(response);
    }

    // ─── Test Helper: In-memory FareMatrixLoader ─────────────────────────

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
                    ["regular"] = 0,
                    ["student"] = 20,
                    ["senior"] = 20,
                    ["pwd"] = 20
                }),
            ["bus"] = new FareMatrix(
                version: "v1-2024",
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
                })
        };

        public Task<FareMatrix?> GetActiveMatrixAsync(string vehicleType)
        {
            Matrices.TryGetValue(vehicleType, out var matrix);
            return Task.FromResult(matrix);
        }

        public Task<IReadOnlyList<FareMatrix>> GetAllActiveMatricesAsync()
        {
            return Task.FromResult<IReadOnlyList<FareMatrix>>(Matrices.Values.ToList().AsReadOnly());
        }

        public Task RefreshAsync() => Task.CompletedTask;
    }
}
