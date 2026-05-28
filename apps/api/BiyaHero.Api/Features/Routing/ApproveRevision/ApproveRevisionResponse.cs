namespace BiyaHero.Api.Features.Routing.ApproveRevision;

/// <summary>
/// Response body for a successfully approved route revision.
/// Returns the revision ID, route ID, revision status, and the updated route details.
/// Requirements: 1.4, 5.8
/// </summary>
public sealed record ApproveRevisionResponse(
    Guid RevisionId,
    Guid RouteId,
    string RevisionStatus,
    ApproveRevisionRouteDto Route);

/// <summary>
/// Updated route details returned after a revision is approved.
/// Includes the new waypoints and verified status.
/// </summary>
public sealed record ApproveRevisionRouteDto(
    Guid Id,
    string Name,
    string VehicleType,
    string Status,
    decimal BaseFare,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ApproveRevisionWaypointDto> Waypoints);

/// <summary>
/// Waypoint DTO for the approve revision response.
/// </summary>
public sealed record ApproveRevisionWaypointDto(
    double Latitude,
    double Longitude,
    int SequenceOrder,
    string? Label);
