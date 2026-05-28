namespace BiyaHero.Api.Features.Routing.SubmitRevision;

/// <summary>
/// Response body for a successfully submitted route revision.
/// Returns the revision ID, route ID, status (Pending), and waypoint count.
/// </summary>
public sealed record SubmitRevisionResponse(
    Guid RevisionId,
    Guid RouteId,
    string Status,
    int WaypointCount);
