namespace BiyaHero.Api.Features.Fare;

/// <summary>
/// Request body for POST /v1/fare/:calculate.
/// Contains origin/destination coordinates, vehicle type, and optional discount category.
/// 
/// Nullable doubles allow distinguishing "field missing" (400) from "field present but invalid" (422).
/// Requirements: 2.1, 2.5, 2.6, 2.7
/// </summary>
public sealed record FareCalculateRequest(
    double? OriginLat,
    double? OriginLng,
    double? DestinationLat,
    double? DestinationLng,
    string? VehicleType,
    string? DiscountCategory = "regular");
