namespace BiyaHero.Api.Features.Routing.ListRoutes;

/// <summary>
/// Route list item DTO — includes full waypoints so the frontend can render routes on the map.
/// </summary>
public sealed record RouteListItemDto(
    Guid Id,
    string Name,
    string VehicleType,
    string Status,
    decimal BaseFare,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int WaypointCount,
    IReadOnlyList<RouteWaypointDto> Waypoints);

/// <summary>Waypoint DTO for list responses.</summary>
public sealed record RouteWaypointDto(
    double Lat,
    double Lng,
    int Position,
    string? Name);
