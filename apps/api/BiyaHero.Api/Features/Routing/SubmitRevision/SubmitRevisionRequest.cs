namespace BiyaHero.Api.Features.Routing.SubmitRevision;

/// <summary>
/// Request body for POST /v1/routes/{id}/revisions.
/// Contains the proposed new waypoints for the route revision.
/// </summary>
public sealed record SubmitRevisionRequest(List<SubmitRevisionWaypointDto> Waypoints);

/// <summary>
/// DTO representing a single waypoint in a revision submission request.
/// </summary>
public sealed record SubmitRevisionWaypointDto(
    double Latitude,
    double Longitude,
    int SequenceOrder,
    string? Label);
