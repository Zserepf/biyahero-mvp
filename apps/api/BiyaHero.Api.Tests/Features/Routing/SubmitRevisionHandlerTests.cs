using System.Linq.Expressions;
using System.Security.Claims;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Features.Routing.SubmitRevision;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Tests.Features.Routing;

public class SubmitRevisionHandlerTests
{
    private readonly FakeJwtService _jwtService = new();
    private readonly FakeRouteRepository _routeRepository = new();
    private readonly FakeRouteRevisionRepository _revisionRepository = new();
    private readonly SubmitRevisionHandler _handler;

    public SubmitRevisionHandlerTests()
    {
        _handler = new SubmitRevisionHandler(_routeRepository, _revisionRepository, _jwtService);
    }

    // ─── Authentication Tests (Req 1.6) ───────────────────────────────────

    [Fact]
    public async Task HandleAsync_MissingAuthHeader_ThrowsUnauthenticatedException()
    {
        var routeId = Guid.NewGuid();
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(routeId, null, request));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("auth.unauthenticated", ex.Code);
    }

    [Fact]
    public async Task HandleAsync_EmptyAuthHeader_ThrowsUnauthenticatedException()
    {
        var routeId = Guid.NewGuid();
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(routeId, "", request));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidToken_ThrowsUnauthenticatedException()
    {
        _jwtService.SetValidationResult(JwtValidationResult.Failure("Token expired."));
        var routeId = Guid.NewGuid();
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(routeId, "Bearer expired-token", request));

        Assert.Equal(401, ex.StatusCode);
    }

    // ─── Route Not Found (404) ────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_RouteNotFound_ThrowsNotFoundException()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        var request = CreateValidRequest();
        // Route repository returns null by default

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.HandleAsync(routeId, "Bearer valid-token", request));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal("resource.not_found", ex.Code);
    }

    // ─── Validation Tests: Waypoint Count (Req 1.7) ───────────────────────

    [Fact]
    public async Task HandleAsync_ZeroWaypoints_ThrowsValidationException()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new SubmitRevisionRequest(new List<SubmitRevisionWaypointDto>());

        var ex = await Assert.ThrowsAsync<SubmitRevisionValidationException>(
            () => _handler.HandleAsync(routeId, "Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Equal("input.validation_failed", ex.Code);
        Assert.Contains("at least 2 waypoints", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_OneWaypoint_ThrowsValidationException()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new SubmitRevisionRequest(new List<SubmitRevisionWaypointDto>
        {
            new(14.5995, 120.9842, 0, "Manila")
        });

        var ex = await Assert.ThrowsAsync<SubmitRevisionValidationException>(
            () => _handler.HandleAsync(routeId, "Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("at least 2 waypoints", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_NullWaypoints_ThrowsValidationException()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new SubmitRevisionRequest(null!);

        var ex = await Assert.ThrowsAsync<SubmitRevisionValidationException>(
            () => _handler.HandleAsync(routeId, "Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("at least 2 waypoints", ex.Message);
    }

    // ─── Validation Tests: Philippines Bounding Box (Req 1.8) ─────────────

    [Fact]
    public async Task HandleAsync_LatitudeBelowMinimum_ThrowsValidationException()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new SubmitRevisionRequest(new List<SubmitRevisionWaypointDto>
        {
            new(4.4, 120.0, 0, "Below min lat"),
            new(14.5, 120.5, 1, "Valid")
        });

        var ex = await Assert.ThrowsAsync<SubmitRevisionValidationException>(
            () => _handler.HandleAsync(routeId, "Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("latitude", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_LatitudeAboveMaximum_ThrowsValidationException()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new SubmitRevisionRequest(new List<SubmitRevisionWaypointDto>
        {
            new(21.6, 120.0, 0, "Above max lat"),
            new(14.5, 120.5, 1, "Valid")
        });

        var ex = await Assert.ThrowsAsync<SubmitRevisionValidationException>(
            () => _handler.HandleAsync(routeId, "Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("latitude", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_LongitudeBelowMinimum_ThrowsValidationException()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new SubmitRevisionRequest(new List<SubmitRevisionWaypointDto>
        {
            new(14.5, 115.9, 0, "Below min lng"),
            new(14.5, 120.5, 1, "Valid")
        });

        var ex = await Assert.ThrowsAsync<SubmitRevisionValidationException>(
            () => _handler.HandleAsync(routeId, "Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("longitude", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_LongitudeAboveMaximum_ThrowsValidationException()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new SubmitRevisionRequest(new List<SubmitRevisionWaypointDto>
        {
            new(14.5, 127.1, 0, "Above max lng"),
            new(14.5, 120.5, 1, "Valid")
        });

        var ex = await Assert.ThrowsAsync<SubmitRevisionValidationException>(
            () => _handler.HandleAsync(routeId, "Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("longitude", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Success Tests (Req 1.3) ──────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidRequest_ReturnsRevisionWithPendingStatus()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = CreateValidRequest();

        var response = await _handler.HandleAsync(routeId, "Bearer valid-token", request);

        Assert.NotEqual(Guid.Empty, response.RevisionId);
        Assert.Equal(routeId, response.RouteId);
        Assert.Equal("Pending", response.Status);
        Assert.Equal(3, response.WaypointCount);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_PersistsRevisionViaRepository()
    {
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, "user@test.com", "Commuter"));
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = CreateValidRequest();

        await _handler.HandleAsync(routeId, "Bearer valid-token", request);

        Assert.Single(_revisionRepository.CreatedRevisions);
        var persisted = _revisionRepository.CreatedRevisions[0];
        Assert.Equal(routeId, persisted.RouteId);
        Assert.Equal(userId, persisted.SubmittedBy);
        Assert.Equal(RevisionStatus.Pending, persisted.Status);
        Assert.Equal(3, persisted.Waypoints.Count);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_PhilippinesBoundaryEdges_Succeeds()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new SubmitRevisionRequest(new List<SubmitRevisionWaypointDto>
        {
            new(4.5, 116.0, 0, "SW corner"),
            new(21.5, 127.0, 1, "NE corner")
        });

        var response = await _handler.HandleAsync(routeId, "Bearer valid-token", request);

        Assert.NotNull(response);
        Assert.Equal("Pending", response.Status);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private void SetupValidAuth()
    {
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(Guid.NewGuid(), "commuter@test.com", "Commuter"));
    }

    private static SubmitRevisionRequest CreateValidRequest()
    {
        return new SubmitRevisionRequest(new List<SubmitRevisionWaypointDto>
        {
            new(14.5547, 121.0244, 0, "Ayala"),
            new(14.5870, 121.0560, 1, "Guadalupe"),
            new(14.6195, 121.0510, 2, "Ortigas")
        });
    }

    private static Route CreateRoute(Guid routeId)
    {
        return new Route(
            id: routeId,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            name: "Existing Route",
            vehicleType: VehicleType.Jeepney,
            status: RouteStatus.Verified,
            createdBy: Guid.NewGuid(),
            baseFare: 13.0m,
            waypoints: new List<Waypoint>
            {
                new(14.5547, 121.0244, 0, "Start"),
                new(14.5870, 121.0560, 1, "End")
            });
    }

    // ─── Fakes ────────────────────────────────────────────────────────────

    private sealed class FakeJwtService : IJwtService
    {
        private JwtValidationResult _validationResult = JwtValidationResult.Failure("Not configured.");

        public void SetValidationResult(JwtValidationResult result) => _validationResult = result;

        public Task<string> GenerateAccessTokenAsync(User user, CancellationToken cancellationToken = default)
            => Task.FromResult("fake-access-token");

        public Task<string> GenerateRefreshTokenAsync(User user, CancellationToken cancellationToken = default)
            => Task.FromResult("fake-refresh-token");

        public Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (_validationResult.IsValid)
            {
                var claims = new List<Claim>
                {
                    new("sub", _validationResult.UserId?.ToString() ?? ""),
                };
                if (_validationResult.Email != null)
                    claims.Add(new("email", _validationResult.Email));
                if (_validationResult.Role != null)
                    claims.Add(new("role", _validationResult.Role));

                var identity = new ClaimsIdentity(claims, "Test");
                return Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(identity));
            }
            return Task.FromResult<ClaimsPrincipal?>(null);
        }

        public Task<Guid?> GetUserIdFromTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (_validationResult.IsValid)
                return Task.FromResult<Guid?>(_validationResult.UserId);
            return Task.FromResult<Guid?>(null);
        }

        public Task<JwtValidationResult> ValidateTokenDetailedAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(_validationResult);
    }

    private sealed class FakeRouteRepository : IRouteRepository
    {
        private Route? _routeToReturn;

        public void SetRouteToReturn(Route route) => _routeToReturn = route;

        public Task<Route?> FindByIdAsync(Guid id) => Task.FromResult(_routeToReturn);
        public Task<Route?> FindByIdWithWaypointsAsync(Guid id) => Task.FromResult(_routeToReturn);
        public Task<Route> CreateAsync(Route entity) => Task.FromResult(entity);
        public Task<IReadOnlyList<Route>> FindAllAsync() => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<IReadOnlyList<Route>> WhereAsync(Expression<Func<Route, bool>> predicate) => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<Route> SaveAsync(Route entity) => Task.FromResult(entity);
        public Task<Route> UpdateAsync(Route entity) => Task.FromResult(entity);
        public Task DeleteAsync(Route entity) => Task.CompletedTask;
        public Task<IReadOnlyList<Route>> FindByBboxAsync(double minLat, double minLon, double maxLat, double maxLon) => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
    }

    private sealed class FakeRouteRevisionRepository : IRouteRevisionRepository
    {
        public List<RouteRevision> CreatedRevisions { get; } = new();

        public Task<RouteRevision> CreateAsync(RouteRevision entity)
        {
            CreatedRevisions.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<RouteRevision?> FindByIdAsync(Guid id) => Task.FromResult<RouteRevision?>(null);
        public Task<IReadOnlyList<RouteRevision>> FindAllAsync() => Task.FromResult<IReadOnlyList<RouteRevision>>(new List<RouteRevision>());
        public Task<IReadOnlyList<RouteRevision>> WhereAsync(Expression<Func<RouteRevision, bool>> predicate) => Task.FromResult<IReadOnlyList<RouteRevision>>(new List<RouteRevision>());
        public Task<RouteRevision> SaveAsync(RouteRevision entity) => Task.FromResult(entity);
        public Task<RouteRevision> UpdateAsync(RouteRevision entity) => Task.FromResult(entity);
        public Task DeleteAsync(RouteRevision entity) => Task.CompletedTask;
        public Task<IReadOnlyList<RouteRevision>> FindByRouteIdAsync(Guid routeId) => Task.FromResult<IReadOnlyList<RouteRevision>>(new List<RouteRevision>());
    }
}
