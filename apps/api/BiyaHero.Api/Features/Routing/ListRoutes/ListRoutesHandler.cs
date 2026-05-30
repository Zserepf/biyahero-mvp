using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Features.Routing.ListRoutes;

/// <summary>
/// Business logic for listing routes within a bounding box (GET /v1/routes).
/// Returns full route data including waypoints so the frontend can render them on the map.
/// Requirements: 1.2
/// </summary>
public sealed class ListRoutesHandler
{
    private readonly IRouteRepository _routeRepository;

    public ListRoutesHandler(IRouteRepository routeRepository)
    {
        _routeRepository = routeRepository;
    }

    public async Task<IReadOnlyList<RouteListItemDto>> HandleAsync(
        double minLat, double minLon, double maxLat, double maxLon)
    {
        var routes = await _routeRepository.FindByBboxAsync(minLat, minLon, maxLat, maxLon);

        return routes.Select(r => new RouteListItemDto(
            Id: r.Id,
            Name: r.Name,
            VehicleType: r.VehicleType.ToString().ToLowerInvariant(),
            Status: r.Status.ToString().ToLowerInvariant(),
            BaseFare: r.BaseFare,
            CreatedBy: r.CreatedBy,
            CreatedAt: r.CreatedAt,
            UpdatedAt: r.UpdatedAt,
            WaypointCount: r.Waypoints.Count,
            Waypoints: r.Waypoints
                .OrderBy(w => w.SequenceOrder)
                .Select(w => new RouteWaypointDto(
                    Lat: w.Latitude,
                    Lng: w.Longitude,
                    Position: w.SequenceOrder,
                    Name: w.Label))
                .ToList()
        )).ToList();
    }
}
