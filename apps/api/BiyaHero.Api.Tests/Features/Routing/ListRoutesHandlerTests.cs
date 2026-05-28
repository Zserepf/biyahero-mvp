using System.Linq.Expressions;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Routing.ListRoutes;
using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Tests.Features.Routing;

public class ListRoutesHandlerTests
{
    private readonly FakeRouteRepository _routeRepository = new();
    private readonly ListRoutesHandler _handler;

    public ListRoutesHandlerTests()
    {
        _handler = new ListRoutesHandler(_routeRepository);
    }

    // ─── Happy Path (Req 1.2) ─────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_RoutesInBbox_ReturnsMatchingRoutes()
    {
        var route1 = CreateRoute(Guid.NewGuid(), "EDSA Route", VehicleType.Jeepney, RouteStatus.Verified);
        var route2 = CreateRoute(Guid.NewGuid(), "Taft Route", VehicleType.Bus, RouteStatus.Unverified);
        _routeRepository.SetBboxResults(new List<Route> { route1, route2 });

        var results = await _handler.HandleAsync(14.0, 120.0, 15.0, 121.0);

        Assert.Equal(2, results.Count);
        Assert.Equal("EDSA Route", results[0].Name);
        Assert.Equal("Taft Route", results[1].Name);
    }

    [Fact]
    public async Task HandleAsync_NoRoutesInBbox_ReturnsEmptyList()
    {
        _routeRepository.SetBboxResults(new List<Route>());

        var results = await _handler.HandleAsync(14.0, 120.0, 15.0, 121.0);

        Assert.Empty(results);
    }

    [Fact]
    public async Task HandleAsync_MapsRouteFieldsCorrectly()
    {
        var routeId = Guid.NewGuid();
        var route = CreateRoute(routeId, "Test Route", VehicleType.UV_Express, RouteStatus.Verified, baseFare: 25.0m, waypointCount: 5);
        _routeRepository.SetBboxResults(new List<Route> { route });

        var results = await _handler.HandleAsync(14.0, 120.0, 15.0, 121.0);

        Assert.Single(results);
        var dto = results[0];
        Assert.Equal(routeId, dto.Id);
        Assert.Equal("Test Route", dto.Name);
        Assert.Equal("UV_Express", dto.VehicleType);
        Assert.Equal("Verified", dto.Status);
        Assert.Equal(25.0m, dto.BaseFare);
        Assert.Equal(5, dto.WaypointCount);
    }

    [Fact]
    public async Task HandleAsync_PassesBboxParametersToRepository()
    {
        _routeRepository.SetBboxResults(new List<Route>());

        await _handler.HandleAsync(4.5, 116.0, 21.5, 127.0);

        Assert.Equal((4.5, 116.0, 21.5, 127.0), _routeRepository.LastBboxQuery);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static Route CreateRoute(
        Guid id, string name, VehicleType vehicleType, RouteStatus status,
        decimal baseFare = 13.0m, int waypointCount = 2)
    {
        var waypoints = Enumerable.Range(0, waypointCount)
            .Select(i => new Waypoint(14.5 + i * 0.01, 121.0 + i * 0.01, i, $"WP{i}"))
            .ToList();

        return new Route(
            id: id,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            name: name,
            vehicleType: vehicleType,
            status: status,
            createdBy: Guid.NewGuid(),
            baseFare: baseFare,
            waypoints: waypoints);
    }

    // ─── Fakes ────────────────────────────────────────────────────────────

    private sealed class FakeRouteRepository : IRouteRepository
    {
        private IReadOnlyList<Route> _bboxResults = new List<Route>();
        public (double, double, double, double)? LastBboxQuery { get; private set; }

        public void SetBboxResults(IReadOnlyList<Route> routes) => _bboxResults = routes;

        public Task<IReadOnlyList<Route>> FindByBboxAsync(double minLat, double minLon, double maxLat, double maxLon)
        {
            LastBboxQuery = (minLat, minLon, maxLat, maxLon);
            return Task.FromResult(_bboxResults);
        }

        public Task<Route?> FindByIdWithWaypointsAsync(Guid id) => Task.FromResult<Route?>(null);
        public Task<Route> CreateAsync(Route entity) => Task.FromResult(entity);
        public Task<Route?> FindByIdAsync(Guid id) => Task.FromResult<Route?>(null);
        public Task<IReadOnlyList<Route>> FindAllAsync() => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<IReadOnlyList<Route>> WhereAsync(Expression<Func<Route, bool>> predicate) => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<Route> SaveAsync(Route entity) => Task.FromResult(entity);
        public Task<Route> UpdateAsync(Route entity) => Task.FromResult(entity);
        public Task DeleteAsync(Route entity) => Task.CompletedTask;
    }
}
