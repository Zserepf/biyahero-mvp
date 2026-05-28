namespace BiyaHero.Api.Features.Routing.CreateRoute;

/// <summary>
/// DTO representing a single waypoint in a route creation request.
/// </summary>
public sealed record CreateRouteWaypointDto(
    double Latitude,
    double Longitude,
    int SequenceOrder,
    string? Label);
