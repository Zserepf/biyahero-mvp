namespace BiyaHero.Api.Features.Routing.CreateRoute;

/// <summary>
/// Request body for POST /v1/routes.
/// Contains the route name, vehicle type, base fare, and ordered waypoints.
/// </summary>
public sealed record CreateRouteRequest(
    string Name,
    string VehicleType,
    decimal BaseFare,
    List<CreateRouteWaypointDto> Waypoints);
