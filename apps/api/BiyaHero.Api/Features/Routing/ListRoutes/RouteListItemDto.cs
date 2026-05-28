namespace BiyaHero.Api.Features.Routing.ListRoutes;

/// <summary>
/// Summary DTO for a route in list responses.
/// Contains only the fields needed for the map/list view — no full waypoint data.
/// </summary>
public sealed record RouteListItemDto(
    Guid Id,
    string Name,
    string VehicleType,
    string Status,
    decimal BaseFare,
    int WaypointCount);
