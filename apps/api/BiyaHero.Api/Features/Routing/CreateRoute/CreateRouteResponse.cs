namespace BiyaHero.Api.Features.Routing.CreateRoute;

/// <summary>
/// Response body for a successfully created route.
/// Returns the new route's ID, name, vehicle type, status, base fare, and waypoint count.
/// </summary>
public sealed record CreateRouteResponse(
    Guid Id,
    string Name,
    string VehicleType,
    string Status,
    decimal BaseFare,
    int WaypointCount);
