using BiyaHero.Api.Domain;
using Route = BiyaHero.Api.Domain.Route;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Repository interface for Route-specific data access.
/// Extends the generic IRepository with spatial bounding-box queries
/// and waypoint-inclusive lookups required by the route browsing flow.
/// </summary>
public interface IRouteRepository : IRepository<Route>
{
    /// <summary>
    /// Finds routes that have at least one waypoint within the given bounding box.
    /// Used by the map view to display routes visible in the current viewport.
    /// </summary>
    Task<IReadOnlyList<Route>> FindByBboxAsync(double minLat, double minLon, double maxLat, double maxLon);

    /// <summary>
    /// Finds a route by ID and eagerly loads its waypoints ordered by sequence.
    /// Returns null if no route exists with the given ID.
    /// </summary>
    Task<Route?> FindByIdWithWaypointsAsync(Guid id);
}
