using System.Linq.Expressions;
using System.Security.Claims;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Features.Routing.VoteRoute;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Tests.Features.Routing;

public class VoteRouteHandlerTests
{
    private readonly FakeJwtService _jwtService = new();
    private readonly FakeRouteRepository _routeRepository = new();
    private readonly FakeRouteVoteRepository _routeVoteRepository = new();
    private readonly VoteRouteHandler _handler;

    public VoteRouteHandlerTests()
    {
        _handler = new VoteRouteHandler(_routeRepository, _routeVoteRepository, _jwtService);
    }

    // ─── Authentication Tests (Req 1.6) ───────────────────────────────────

    [Fact]
    public async Task HandleAsync_MissingAuthHeader_ThrowsUnauthenticatedException()
    {
        var routeId = Guid.NewGuid();
        var request = new VoteRouteRequest("still_accurate");

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(routeId, null, request));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("auth.unauthenticated", ex.Code);
    }

    [Fact]
    public async Task HandleAsync_EmptyAuthHeader_ThrowsUnauthenticatedException()
    {
        var routeId = Guid.NewGuid();
        var request = new VoteRouteRequest("still_accurate");

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(routeId, "", request));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidToken_ThrowsUnauthenticatedException()
    {
        _jwtService.SetValidationResult(JwtValidationResult.Failure("Token expired."));
        var routeId = Guid.NewGuid();
        var request = new VoteRouteRequest("still_accurate");

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(routeId, "Bearer expired-token", request));

        Assert.Equal(401, ex.StatusCode);
    }

    // ─── Route Not Found (404) ────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_RouteNotFound_ReturnsNull()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        var request = new VoteRouteRequest("still_accurate");

        // Route repository returns null by default (route not found)
        var result = await _handler.HandleAsync(routeId, "Bearer valid-token", request);

        Assert.Null(result);
    }

    // ─── Validation Tests: Vote Kind (422) ────────────────────────────────

    [Fact]
    public async Task HandleAsync_InvalidVoteKind_ThrowsValidationException()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new VoteRouteRequest("invalid_kind");

        var ex = await Assert.ThrowsAsync<VoteRouteValidationException>(
            () => _handler.HandleAsync(routeId, "Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Equal("input.validation_failed", ex.Code);
        Assert.Contains("still_accurate", ex.Message);
        Assert.Contains("no_longer_accurate", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_EmptyVoteKind_ThrowsValidationException()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new VoteRouteRequest("");

        var ex = await Assert.ThrowsAsync<VoteRouteValidationException>(
            () => _handler.HandleAsync(routeId, "Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_NullVoteKind_ThrowsValidationException()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new VoteRouteRequest(null!);

        var ex = await Assert.ThrowsAsync<VoteRouteValidationException>(
            () => _handler.HandleAsync(routeId, "Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
    }

    // ─── Duplicate Vote (409) ─────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_DuplicateVote_ThrowsConflictException()
    {
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, "user@test.com", "Commuter"));
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        _routeVoteRepository.SetExistsResult(true); // Simulate existing vote
        var request = new VoteRouteRequest("still_accurate");

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.HandleAsync(routeId, "Bearer valid-token", request));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("resource.conflict", ex.Code);
        Assert.Contains("already voted", ex.Message);
    }

    // ─── Success Tests (Req 1.5) ──────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_StillAccurate_ReturnsCreatedVote()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new VoteRouteRequest("still_accurate");

        var response = await _handler.HandleAsync(routeId, "Bearer valid-token", request);

        Assert.NotNull(response);
        Assert.NotEqual(Guid.Empty, response.VoteId);
        Assert.Equal(routeId, response.RouteId);
        Assert.Equal("still_accurate", response.Kind);
        Assert.Equal("Vote recorded successfully.", response.Message);
    }

    [Fact]
    public async Task HandleAsync_NoLongerAccurate_ReturnsCreatedVote()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new VoteRouteRequest("no_longer_accurate");

        var response = await _handler.HandleAsync(routeId, "Bearer valid-token", request);

        Assert.NotNull(response);
        Assert.Equal(routeId, response.RouteId);
        Assert.Equal("no_longer_accurate", response.Kind);
    }

    [Fact]
    public async Task HandleAsync_ValidVote_PersistsViaRepository()
    {
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, "user@test.com", "Commuter"));
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new VoteRouteRequest("still_accurate");

        await _handler.HandleAsync(routeId, "Bearer valid-token", request);

        Assert.Single(_routeVoteRepository.CreatedVotes);
        var persisted = _routeVoteRepository.CreatedVotes[0];
        Assert.Equal(routeId, persisted.RouteId);
        Assert.Equal(userId, persisted.VoterId);
        Assert.Equal(VoteKind.StillAccurate, persisted.Kind);
    }

    [Fact]
    public async Task HandleAsync_VoteKindCaseInsensitive_Succeeds()
    {
        SetupValidAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        var request = new VoteRouteRequest("STILL_ACCURATE");

        var response = await _handler.HandleAsync(routeId, "Bearer valid-token", request);

        Assert.NotNull(response);
        Assert.Equal("STILL_ACCURATE", response.Kind);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private void SetupValidAuth()
    {
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(Guid.NewGuid(), "commuter@test.com", "Commuter"));
    }

    private static Route CreateRoute(Guid routeId)
    {
        return new Route(
            id: routeId,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            name: "Test Route",
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

        public Task<Route> CreateAsync(Route entity) => Task.FromResult(entity);
        public Task<Route?> FindByIdAsync(Guid id) => Task.FromResult(_routeToReturn);
        public Task<IReadOnlyList<Route>> FindAllAsync() => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<IReadOnlyList<Route>> WhereAsync(Expression<Func<Route, bool>> predicate) => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<Route> SaveAsync(Route entity) => Task.FromResult(entity);
        public Task<Route> UpdateAsync(Route entity) => Task.FromResult(entity);
        public Task DeleteAsync(Route entity) => Task.CompletedTask;
        public Task<IReadOnlyList<Route>> FindByBboxAsync(double minLat, double minLon, double maxLat, double maxLon) => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<Route?> FindByIdWithWaypointsAsync(Guid id) => Task.FromResult(_routeToReturn);
    }

    private sealed class FakeRouteVoteRepository : IRouteVoteRepository
    {
        private bool _existsResult;
        public List<RouteVote> CreatedVotes { get; } = new();

        public void SetExistsResult(bool exists) => _existsResult = exists;

        public Task<bool> ExistsAsync(Guid routeId, Guid voterId) => Task.FromResult(_existsResult);

        public Task<RouteVote> CreateAsync(RouteVote entity)
        {
            CreatedVotes.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<RouteVote?> FindByIdAsync(Guid id) => Task.FromResult<RouteVote?>(null);
        public Task<IReadOnlyList<RouteVote>> FindAllAsync() => Task.FromResult<IReadOnlyList<RouteVote>>(new List<RouteVote>());
        public Task<IReadOnlyList<RouteVote>> WhereAsync(Expression<Func<RouteVote, bool>> predicate) => Task.FromResult<IReadOnlyList<RouteVote>>(new List<RouteVote>());
        public Task<RouteVote> SaveAsync(RouteVote entity) => Task.FromResult(entity);
        public Task<RouteVote> UpdateAsync(RouteVote entity) => Task.FromResult(entity);
        public Task DeleteAsync(RouteVote entity) => Task.CompletedTask;
    }
}
