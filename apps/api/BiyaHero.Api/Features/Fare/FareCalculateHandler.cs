namespace BiyaHero.Api.Features.Fare;

/// <summary>
/// Business logic for fare calculation (POST /v1/fare/:calculate).
/// Loads the fare matrix for the requested vehicle type, computes the fare
/// using FareCalculator.Calculate, and returns the result.
/// 
/// Requirements: 2.1, 2.6, 2.7, 2.9
/// </summary>
public sealed class FareCalculateHandler
{
    private readonly IFareMatrixLoader _matrixLoader;

    public FareCalculateHandler(IFareMatrixLoader matrixLoader)
    {
        _matrixLoader = matrixLoader;
    }

    /// <summary>
    /// Handles the fare calculation request.
    /// Returns null if the vehicle type is not found (caller should return 422).
    /// 
    /// Precondition: All required fields on the request are non-null (validated by endpoint).
    /// </summary>
    public async Task<FareCalculateResponse?> HandleAsync(FareCalculateRequest request)
    {
        var matrix = await _matrixLoader.GetActiveMatrixAsync(request.VehicleType!);

        if (matrix is null)
        {
            return null;
        }

        var discountCategory = string.IsNullOrWhiteSpace(request.DiscountCategory)
            ? "regular"
            : request.DiscountCategory.Trim().ToLowerInvariant();

        var result = FareCalculator.Calculate(
            request.OriginLat!.Value,
            request.OriginLng!.Value,
            request.DestinationLat!.Value,
            request.DestinationLng!.Value,
            matrix,
            discountCategory);

        return new FareCalculateResponse(
            AmountPhp: result.AmountPhp,
            DistanceKm: result.DistanceKm,
            MatrixVersion: result.MatrixVersion,
            VehicleType: matrix.VehicleType,
            DiscountCategory: discountCategory,
            DiscountApplied: discountCategory != "regular");
    }
}
