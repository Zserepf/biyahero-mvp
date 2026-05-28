using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Features.Routing.GetRoute;

/// <summary>
/// Business logic for fetching a single route with full waypoints (GET /v1/routes/{id}).
/// Delegates to the RouteRepository and maps the result to a detail DTO.
/// Requirements: 1.2
/// </summary>
public sealed class GetRouteHandler
{
    private readonly IRouteRepository _routeRepository;

    public GetRouteHandler(IRouteRepository routeRepository)
    {
        _routeRepository = routeRepository;
    }

    /// <summary>
    /// Fetches a route by ID with its full waypoint list.
    /// Returns null if the route does not exist.
    /// </summary>
    public async Task<RouteDetailDto?> HandleAsync(Guid id)
    {
        var route = await _routeRepository.FindByIdWithWaypointsAsync(id);
        if (route is null)
            return null;

        var waypoints = route.Waypoints
            .OrderBy(w => w.SequenceOrder)
            .Select(w => new WaypointDto(
                Latitude: w.Latitude,
                Longitude: w.Longitude,
                SequenceOrder: w.SequenceOrder,
                Label: w.Label))
            .ToList();

        return new RouteDetailDto(
            Id: route.Id,
            Name: route.Name,
            VehicleType: route.VehicleType.ToString(),
            Status: route.Status.ToString(),
            BaseFare: route.BaseFare,
            CreatedBy: route.CreatedBy,
            CreatedAt: route.CreatedAt,
            UpdatedAt: route.UpdatedAt,
            Waypoints: waypoints);
    }
}
