using System.Linq.Expressions;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Routing.GetRoute;
using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Tests.Features.Routing;

public class GetRouteHandlerTests
{
    private readonly FakeRouteRepository _routeRepository = new();
    private readonly GetRouteHandler _handler;

    public GetRouteHandlerTests()
    {
        _handler = new GetRouteHandler(_routeRepository);
    }

    // ─── Happy Path (Req 1.2) ─────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ExistingRoute_ReturnsRouteDetail()
    {
        var routeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var route = CreateRoute(routeId, ownerId, "EDSA Route", VehicleType.Jeepney, RouteStatus.Verified);
        _routeRepository.SetRouteToReturn(route);

        var result = await _handler.HandleAsync(routeId);

        Assert.NotNull(result);
        Assert.Equal(routeId, result.Id);
        Assert.Equal("EDSA Route", result.Name);
        Assert.Equal("Jeepney", result.VehicleType);
        Assert.Equal("Verified", result.Status);
        Assert.Equal(13.0m, result.BaseFare);
        Assert.Equal(ownerId, result.CreatedBy);
    }

    [Fact]
    public async Task HandleAsync_ExistingRoute_ReturnsWaypointsOrderedBySequence()
    {
        var routeId = Guid.NewGuid();
        var waypoints = new List<Waypoint>
        {
            new(14.6195, 121.0510, 2, "Ortigas"),
            new(14.5547, 121.0244, 0, "Ayala"),
            new(14.5870, 121.0560, 1, "Guadalupe")
        };
        var route = new Route(
            id: routeId,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            name: "EDSA Route",
            vehicleType: VehicleType.Jeepney,
            status: RouteStatus.Verified,
            createdBy: Guid.NewGuid(),
            baseFare: 13.0m,
            waypoints: waypoints);
        _routeRepository.SetRouteToReturn(route);

        var result = await _handler.HandleAsync(routeId);

        Assert.NotNull(result);
        Assert.Equal(3, result.Waypoints.Count);
        Assert.Equal(0, result.Waypoints[0].SequenceOrder);
        Assert.Equal("Ayala", result.Waypoints[0].Label);
        Assert.Equal(1, result.Waypoints[1].SequenceOrder);
        Assert.Equal("Guadalupe", result.Waypoints[1].Label);
        Assert.Equal(2, result.Waypoints[2].SequenceOrder);
        Assert.Equal("Ortigas", result.Waypoints[2].Label);
    }

    [Fact]
    public async Task HandleAsync_ExistingRoute_MapsWaypointCoordinatesCorrectly()
    {
        var routeId = Guid.NewGuid();
        var route = CreateRoute(routeId, Guid.NewGuid(), "Test", VehicleType.Bus, RouteStatus.Unverified);
        _routeRepository.SetRouteToReturn(route);

        var result = await _handler.HandleAsync(routeId);

        Assert.NotNull(result);
        Assert.Equal(14.5547, result.Waypoints[0].Latitude);
        Assert.Equal(121.0244, result.Waypoints[0].Longitude);
    }

    // ─── Route Not Found (404) ────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NonExistentRoute_ReturnsNull()
    {
        var result = await _handler.HandleAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static Route CreateRoute(
        Guid id, Guid ownerId, string name, VehicleType vehicleType, RouteStatus status)
    {
        return new Route(
            id: id,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            name: name,
            vehicleType: vehicleType,
            status: status,
            createdBy: ownerId,
            baseFare: 13.0m,
            waypoints: new List<Waypoint>
            {
                new(14.5547, 121.0244, 0, "Ayala"),
                new(14.5870, 121.0560, 1, "Guadalupe"),
                new(14.6195, 121.0510, 2, "Ortigas")
            });
    }

    // ─── Fakes ────────────────────────────────────────────────────────────

    private sealed class FakeRouteRepository : IRouteRepository
    {
        private Route? _routeToReturn;

        public void SetRouteToReturn(Route route) => _routeToReturn = route;

        public Task<Route?> FindByIdWithWaypointsAsync(Guid id) => Task.FromResult(_routeToReturn);
        public Task<IReadOnlyList<Route>> FindByBboxAsync(double minLat, double minLon, double maxLat, double maxLon)
            => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<Route> CreateAsync(Route entity) => Task.FromResult(entity);
        public Task<Route?> FindByIdAsync(Guid id) => Task.FromResult<Route?>(null);
        public Task<IReadOnlyList<Route>> FindAllAsync() => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<IReadOnlyList<Route>> WhereAsync(Expression<Func<Route, bool>> predicate) => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<Route> SaveAsync(Route entity) => Task.FromResult(entity);
        public Task<Route> UpdateAsync(Route entity) => Task.FromResult(entity);
        public Task DeleteAsync(Route entity) => Task.CompletedTask;
    }
}
