using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Features.Routing.ListRoutes;

/// <summary>
/// Business logic for listing routes within a bounding box (GET /v1/routes).
/// Delegates spatial query to the RouteRepository and maps results to summary DTOs.
/// Requirements: 1.2
/// </summary>
public sealed class ListRoutesHandler
{
    private readonly IRouteRepository _routeRepository;

    public ListRoutesHandler(IRouteRepository routeRepository)
    {
        _routeRepository = routeRepository;
    }

    /// <summary>
    /// Queries routes whose waypoints intersect the given bounding box.
    /// Returns a list of route summaries (no full waypoint data).
    /// </summary>
    public async Task<IReadOnlyList<RouteListItemDto>> HandleAsync(
        double minLat, double minLon, double maxLat, double maxLon)
    {
        var routes = await _routeRepository.FindByBboxAsync(minLat, minLon, maxLat, maxLon);

        return routes.Select(r => new RouteListItemDto(
            Id: r.Id,
            Name: r.Name,
            VehicleType: r.VehicleType.ToString(),
            Status: r.Status.ToString(),
            BaseFare: r.BaseFare,
            WaypointCount: r.Waypoints.Count
        )).ToList();
    }
}
