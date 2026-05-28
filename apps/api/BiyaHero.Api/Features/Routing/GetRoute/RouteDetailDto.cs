namespace BiyaHero.Api.Features.Routing.GetRoute;

/// <summary>
/// Full route detail DTO including all waypoints.
/// Returned by GET /v1/routes/{id}.
/// </summary>
public sealed record RouteDetailDto(
    Guid Id,
    string Name,
    string VehicleType,
    string Status,
    decimal BaseFare,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WaypointDto> Waypoints);

/// <summary>
/// Waypoint DTO for route detail responses.
/// </summary>
public sealed record WaypointDto(
    double Latitude,
    double Longitude,
    int SequenceOrder,
    string? Label);
