using System.Linq.Expressions;
using System.Security.Claims;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Features.Routing.CreateRoute;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Tests.Features.Routing;

public class CreateRouteHandlerTests
{
    private readonly FakeJwtService _jwtService = new();
    private readonly FakeRouteRepository _routeRepository = new();
    private readonly CreateRouteHandler _handler;

    public CreateRouteHandlerTests()
    {
        _handler = new CreateRouteHandler(_routeRepository, _jwtService);
    }

    // ─── Authentication Tests (Req 1.6) ───────────────────────────────────

    [Fact]
    public async Task HandleAsync_MissingAuthHeader_ThrowsUnauthenticatedException()
    {
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(null, request));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("auth.unauthenticated", ex.Code);
    }

    [Fact]
    public async Task HandleAsync_EmptyAuthHeader_ThrowsUnauthenticatedException()
    {
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync("", request));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_NonBearerScheme_ThrowsUnauthenticatedException()
    {
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync("Basic dXNlcjpwYXNz", request));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidToken_ThrowsUnauthenticatedException()
    {
        _jwtService.SetValidationResult(JwtValidationResult.Failure("Token expired."));
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync("Bearer expired-token", request));

        Assert.Equal(401, ex.StatusCode);
    }

    // ─── Validation Tests: Waypoint Count (Req 1.7) ───────────────────────

    [Fact]
    public async Task HandleAsync_ZeroWaypoints_ThrowsValidationException()
    {
        SetupValidAuth();
        var request = new CreateRouteRequest("Test Route", "Jeepney", 12.0m, new List<CreateRouteWaypointDto>());

        var ex = await Assert.ThrowsAsync<CreateRouteValidationException>(
            () => _handler.HandleAsync("Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Equal("input.validation_failed", ex.Code);
        Assert.Contains("at least 2 waypoints", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_OneWaypoint_ThrowsValidationException()
    {
        SetupValidAuth();
        var request = new CreateRouteRequest("Test Route", "Jeepney", 12.0m, new List<CreateRouteWaypointDto>
        {
            new(14.5995, 120.9842, 0, "Manila")
        });

        var ex = await Assert.ThrowsAsync<CreateRouteValidationException>(
            () => _handler.HandleAsync("Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("at least 2 waypoints", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_NullWaypoints_ThrowsValidationException()
    {
        SetupValidAuth();
        var request = new CreateRouteRequest("Test Route", "Jeepney", 12.0m, null!);

        var ex = await Assert.ThrowsAsync<CreateRouteValidationException>(
            () => _handler.HandleAsync("Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("at least 2 waypoints", ex.Message);
    }

    // ─── Validation Tests: Philippines Bounding Box (Req 1.8) ─────────────

    [Fact]
    public async Task HandleAsync_LatitudeBelowMinimum_ThrowsValidationException()
    {
        SetupValidAuth();
        var request = new CreateRouteRequest("Test Route", "Jeepney", 12.0m, new List<CreateRouteWaypointDto>
        {
            new(4.4, 120.0, 0, "Below min lat"),  // lat 4.4 < 4.5
            new(14.5, 120.5, 1, "Valid")
        });

        var ex = await Assert.ThrowsAsync<CreateRouteValidationException>(
            () => _handler.HandleAsync("Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("latitude", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_LatitudeAboveMaximum_ThrowsValidationException()
    {
        SetupValidAuth();
        var request = new CreateRouteRequest("Test Route", "Jeepney", 12.0m, new List<CreateRouteWaypointDto>
        {
            new(21.6, 120.0, 0, "Above max lat"),  // lat 21.6 > 21.5
            new(14.5, 120.5, 1, "Valid")
        });

        var ex = await Assert.ThrowsAsync<CreateRouteValidationException>(
            () => _handler.HandleAsync("Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("latitude", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_LongitudeBelowMinimum_ThrowsValidationException()
    {
        SetupValidAuth();
        var request = new CreateRouteRequest("Test Route", "Jeepney", 12.0m, new List<CreateRouteWaypointDto>
        {
            new(14.5, 115.9, 0, "Below min lng"),  // lng 115.9 < 116.0
            new(14.5, 120.5, 1, "Valid")
        });

        var ex = await Assert.ThrowsAsync<CreateRouteValidationException>(
            () => _handler.HandleAsync("Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("longitude", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_LongitudeAboveMaximum_ThrowsValidationException()
    {
        SetupValidAuth();
        var request = new CreateRouteRequest("Test Route", "Jeepney", 12.0m, new List<CreateRouteWaypointDto>
        {
            new(14.5, 127.1, 0, "Above max lng"),  // lng 127.1 > 127.0
            new(14.5, 120.5, 1, "Valid")
        });

        var ex = await Assert.ThrowsAsync<CreateRouteValidationException>(
            () => _handler.HandleAsync("Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("longitude", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Validation Tests: PH-wide acceptance (Req 1.8) ──────────────────

    [Fact]
    public async Task HandleAsync_WaypointsAtPhilippinesBoundaryEdges_Succeeds()
    {
        // Waypoints at the exact boundary edges should be accepted
        SetupValidAuth();
        var request = new CreateRouteRequest("Boundary Route", "Jeepney", 10.0m, new List<CreateRouteWaypointDto>
        {
            new(4.5, 116.0, 0, "SW corner"),   // min lat, min lng
            new(21.5, 127.0, 1, "NE corner")   // max lat, max lng
        });

        var response = await _handler.HandleAsync("Bearer valid-token", request);

        Assert.NotNull(response);
        Assert.Equal("Boundary Route", response.Name);
    }

    [Fact]
    public async Task HandleAsync_WaypointsInMindanao_Succeeds()
    {
        // Mindanao coordinates — outside Metro Manila but within PH bbox
        SetupValidAuth();
        var request = new CreateRouteRequest("Davao Route", "Jeepney", 9.0m, new List<CreateRouteWaypointDto>
        {
            new(7.0707, 125.6087, 0, "Davao City"),
            new(7.1907, 125.4553, 1, "Toril")
        });

        var response = await _handler.HandleAsync("Bearer valid-token", request);

        Assert.NotNull(response);
        Assert.Equal("Davao Route", response.Name);
    }

    // ─── Success Tests (Req 1.1) ──────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidRequest_ReturnsCreatedRouteWithUnverifiedStatus()
    {
        SetupValidAuth();
        var request = CreateValidRequest();

        var response = await _handler.HandleAsync("Bearer valid-token", request);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("EDSA Route", response.Name);
        Assert.Equal("Jeepney", response.VehicleType);
        Assert.Equal("Unverified", response.Status);
        Assert.Equal(13.0m, response.BaseFare);
        Assert.Equal(3, response.WaypointCount);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_PersistsRouteViaRepository()
    {
        SetupValidAuth();
        var request = CreateValidRequest();

        await _handler.HandleAsync("Bearer valid-token", request);

        Assert.Single(_routeRepository.CreatedRoutes);
        var persisted = _routeRepository.CreatedRoutes[0];
        Assert.Equal("EDSA Route", persisted.Name);
        Assert.Equal(RouteStatus.Unverified, persisted.Status);
        Assert.Equal(3, persisted.Waypoints.Count);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_SetsCreatedByToAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, "user@test.com", "Commuter"));
        var request = CreateValidRequest();

        await _handler.HandleAsync("Bearer valid-token", request);

        var persisted = _routeRepository.CreatedRoutes[0];
        Assert.Equal(userId, persisted.CreatedBy);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_TwoWaypoints_Succeeds()
    {
        SetupValidAuth();
        var request = new CreateRouteRequest("Short Route", "Bus", 15.0m, new List<CreateRouteWaypointDto>
        {
            new(14.5995, 120.9842, 0, "Start"),
            new(14.6500, 121.0500, 1, "End")
        });

        var response = await _handler.HandleAsync("Bearer valid-token", request);

        Assert.NotNull(response);
        Assert.Equal("Short Route", response.Name);
        Assert.Equal("Bus", response.VehicleType);
        Assert.Equal(2, response.WaypointCount);
    }

    // ─── Validation Tests: Vehicle Type ───────────────────────────────────

    [Fact]
    public async Task HandleAsync_InvalidVehicleType_ThrowsValidationException()
    {
        SetupValidAuth();
        var request = new CreateRouteRequest("Test Route", "Helicopter", 12.0m, new List<CreateRouteWaypointDto>
        {
            new(14.5, 120.5, 0, "A"),
            new(14.6, 120.6, 1, "B")
        });

        var ex = await Assert.ThrowsAsync<CreateRouteValidationException>(
            () => _handler.HandleAsync("Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("vehicle type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_EmptyName_ThrowsValidationException()
    {
        SetupValidAuth();
        var request = new CreateRouteRequest("", "Jeepney", 12.0m, new List<CreateRouteWaypointDto>
        {
            new(14.5, 120.5, 0, "A"),
            new(14.6, 120.6, 1, "B")
        });

        var ex = await Assert.ThrowsAsync<CreateRouteValidationException>(
            () => _handler.HandleAsync("Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private void SetupValidAuth()
    {
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(Guid.NewGuid(), "commuter@test.com", "Commuter"));
    }

    private static CreateRouteRequest CreateValidRequest()
    {
        return new CreateRouteRequest(
            Name: "EDSA Route",
            VehicleType: "Jeepney",
            BaseFare: 13.0m,
            Waypoints: new List<CreateRouteWaypointDto>
            {
                new(14.5547, 121.0244, 0, "Ayala"),
                new(14.5870, 121.0560, 1, "Guadalupe"),
                new(14.6195, 121.0510, 2, "Ortigas")
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
        public List<Route> CreatedRoutes { get; } = new();

        public Task<Route> CreateAsync(Route entity)
        {
            CreatedRoutes.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<Route?> FindByIdAsync(Guid id) => Task.FromResult<Route?>(null);
        public Task<IReadOnlyList<Route>> FindAllAsync() => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<IReadOnlyList<Route>> WhereAsync(Expression<Func<Route, bool>> predicate) => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<Route> SaveAsync(Route entity) => Task.FromResult(entity);
        public Task<Route> UpdateAsync(Route entity) => Task.FromResult(entity);
        public Task DeleteAsync(Route entity) => Task.CompletedTask;
        public Task<IReadOnlyList<Route>> FindByBboxAsync(double minLat, double minLon, double maxLat, double maxLon) => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<Route?> FindByIdWithWaypointsAsync(Guid id) => Task.FromResult<Route?>(null);
    }
}
