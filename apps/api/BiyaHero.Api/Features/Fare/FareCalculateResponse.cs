namespace BiyaHero.Api.Features.Fare;

/// <summary>
/// Response body for POST /v1/fare/:calculate.
/// Contains the computed fare, distance, matrix version, vehicle type, and discount info.
/// Requirements: 2.1, 2.5
/// </summary>
public sealed record FareCalculateResponse(
    decimal AmountPhp,
    double DistanceKm,
    string MatrixVersion,
    string VehicleType,
    string DiscountCategory,
    bool DiscountApplied);
