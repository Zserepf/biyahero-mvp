using System.Linq.Expressions;
using System.Security.Claims;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Features.Routing.ApproveRevision;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Tests.Features.Routing;

public class ApproveRevisionHandlerTests
{
    private readonly FakeJwtService _jwtService = new();
    private readonly FakeRouteRepository _routeRepository = new();
    private readonly FakeRouteRevisionRepository _revisionRepository = new();
    private readonly ApproveRevisionHandler _handler;

    public ApproveRevisionHandlerTests()
    {
        _handler = new ApproveRevisionHandler(_routeRepository, _revisionRepository, _jwtService);
    }

    // ─── Authentication Tests (Req 1.6) ───────────────────────────────────

    [Fact]
    public async Task HandleAsync_MissingAuthHeader_ThrowsUnauthenticatedException()
    {
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(null, Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("auth.unauthenticated", ex.Code);
    }

    [Fact]
    public async Task HandleAsync_EmptyAuthHeader_ThrowsUnauthenticatedException()
    {
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync("", Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidToken_ThrowsUnauthenticatedException()
    {
        _jwtService.SetValidationResult(JwtValidationResult.Failure("Token expired."));

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync("Bearer expired-token", Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(401, ex.StatusCode);
    }

    // ─── Authorization Tests: Moderator-Only (Req 5.8) ────────────────────

    [Fact]
    public async Task HandleAsync_CommuterRole_ThrowsForbiddenException()
    {
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(Guid.NewGuid(), "commuter@test.com", "Commuter"));

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.HandleAsync("Bearer valid-token", Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal("auth.forbidden", ex.Code);
        Assert.Contains("Moderator", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_DriverRole_ThrowsForbiddenException()
    {
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(Guid.NewGuid(), "driver@test.com", "Driver"));

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.HandleAsync("Bearer valid-token", Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
        Assert.Contains("Moderator", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_NullRole_ThrowsForbiddenException()
    {
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(Guid.NewGuid(), "user@test.com", null));

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.HandleAsync("Bearer valid-token", Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
    }

    // ─── Route Not Found (404) ────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_RouteNotFound_ThrowsNotFoundException()
    {
        SetupModeratorAuth();
        // Route repository returns null by default

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.HandleAsync("Bearer valid-token", Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal("resource.not_found", ex.Code);
    }

    // ─── Revision Not Found (404) ─────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_RevisionNotFound_ThrowsNotFoundException()
    {
        SetupModeratorAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));
        // Revision repository returns null by default

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.HandleAsync("Bearer valid-token", routeId, Guid.NewGuid()));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_RevisionBelongsToDifferentRoute_ThrowsNotFoundException()
    {
        SetupModeratorAuth();
        var routeId = Guid.NewGuid();
        var otherRouteId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));

        var revision = CreateRevision(Guid.NewGuid(), otherRouteId, RevisionStatus.Pending);
        _revisionRepository.SetRevisionToReturn(revision);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.HandleAsync("Bearer valid-token", routeId, revision.Id));

        Assert.Equal(404, ex.StatusCode);
    }

    // ─── Revision Status Conflict (409) ───────────────────────────────────

    [Fact]
    public async Task HandleAsync_AlreadyApprovedRevision_ThrowsConflictException()
    {
        SetupModeratorAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));

        var revision = CreateRevision(Guid.NewGuid(), routeId, RevisionStatus.Approved);
        _revisionRepository.SetRevisionToReturn(revision);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.HandleAsync("Bearer valid-token", routeId, revision.Id));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("Approved", ex.Message);
        Assert.Contains("cannot be approved", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_RejectedRevision_ThrowsConflictException()
    {
        SetupModeratorAuth();
        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));

        var revision = CreateRevision(Guid.NewGuid(), routeId, RevisionStatus.Rejected);
        _revisionRepository.SetRevisionToReturn(revision);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.HandleAsync("Bearer valid-token", routeId, revision.Id));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("Rejected", ex.Message);
    }

    // ─── Success Tests: Moderator Approval (Req 1.4) ──────────────────────

    [Fact]
    public async Task HandleAsync_ModeratorApprovesPendingRevision_ReturnsApprovedResponse()
    {
        var moderatorId = Guid.NewGuid();
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(moderatorId, "mod@test.com", "Moderator"));

        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));

        var revisionId = Guid.NewGuid();
        var revision = CreateRevision(revisionId, routeId, RevisionStatus.Pending);
        _revisionRepository.SetRevisionToReturn(revision);

        var response = await _handler.HandleAsync("Bearer valid-token", routeId, revisionId);

        Assert.Equal(revisionId, response.RevisionId);
        Assert.Equal(routeId, response.RouteId);
        Assert.Equal("Approved", response.RevisionStatus);
    }

    [Fact]
    public async Task HandleAsync_ModeratorApproves_RouteStatusBecomesVerified()
    {
        SetupModeratorAuth();
        var routeId = Guid.NewGuid();
        var route = CreateRoute(routeId);
        _routeRepository.SetRouteToReturn(route);

        var revision = CreateRevision(Guid.NewGuid(), routeId, RevisionStatus.Pending);
        _revisionRepository.SetRevisionToReturn(revision);

        var response = await _handler.HandleAsync("Bearer valid-token", routeId, revision.Id);

        Assert.Equal("Verified", response.Route.Status);
    }

    [Fact]
    public async Task HandleAsync_ModeratorApproves_RouteWaypointsReplacedWithRevisionWaypoints()
    {
        SetupModeratorAuth();
        var routeId = Guid.NewGuid();
        var route = CreateRoute(routeId);
        _routeRepository.SetRouteToReturn(route);

        var revisionWaypoints = new List<Waypoint>
        {
            new(7.0707, 125.6087, 0, "Davao"),
            new(7.1907, 125.4553, 1, "Toril"),
            new(7.2500, 125.5000, 2, "Calinan")
        };
        var revision = new RouteRevision(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            routeId: routeId,
            submittedBy: Guid.NewGuid(),
            status: RevisionStatus.Pending,
            waypoints: revisionWaypoints);
        _revisionRepository.SetRevisionToReturn(revision);

        var response = await _handler.HandleAsync("Bearer valid-token", routeId, revision.Id);

        Assert.Equal(3, response.Route.Waypoints.Count);
        Assert.Equal("Davao", response.Route.Waypoints[0].Label);
        Assert.Equal("Toril", response.Route.Waypoints[1].Label);
        Assert.Equal("Calinan", response.Route.Waypoints[2].Label);
    }

    [Fact]
    public async Task HandleAsync_ModeratorApproves_PersistsRouteUpdate()
    {
        SetupModeratorAuth();
        var routeId = Guid.NewGuid();
        var route = CreateRoute(routeId);
        _routeRepository.SetRouteToReturn(route);

        var revision = CreateRevision(Guid.NewGuid(), routeId, RevisionStatus.Pending);
        _revisionRepository.SetRevisionToReturn(revision);

        await _handler.HandleAsync("Bearer valid-token", routeId, revision.Id);

        Assert.Single(_routeRepository.UpdatedRoutes);
        var updatedRoute = _routeRepository.UpdatedRoutes[0];
        Assert.Equal(RouteStatus.Verified, updatedRoute.Status);
    }

    [Fact]
    public async Task HandleAsync_ModeratorApproves_PersistsRevisionStatusUpdate()
    {
        var moderatorId = Guid.NewGuid();
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(moderatorId, "mod@test.com", "Moderator"));

        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));

        var revision = CreateRevision(Guid.NewGuid(), routeId, RevisionStatus.Pending);
        _revisionRepository.SetRevisionToReturn(revision);

        await _handler.HandleAsync("Bearer valid-token", routeId, revision.Id);

        Assert.Single(_revisionRepository.UpdatedRevisions);
        var updatedRevision = _revisionRepository.UpdatedRevisions[0];
        Assert.Equal(RevisionStatus.Approved, updatedRevision.Status);
        Assert.Equal(moderatorId, updatedRevision.ApproverId);
    }

    // ─── SuperAdmin Can Also Approve (Req 5.9) ───────────────────────────

    [Fact]
    public async Task HandleAsync_SuperAdminApprovesPendingRevision_Succeeds()
    {
        var adminId = Guid.NewGuid();
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(adminId, "admin@test.com", "SuperAdmin"));

        var routeId = Guid.NewGuid();
        _routeRepository.SetRouteToReturn(CreateRoute(routeId));

        var revision = CreateRevision(Guid.NewGuid(), routeId, RevisionStatus.Pending);
        _revisionRepository.SetRevisionToReturn(revision);

        var response = await _handler.HandleAsync("Bearer valid-token", routeId, revision.Id);

        Assert.Equal("Approved", response.RevisionStatus);
        Assert.Equal("Verified", response.Route.Status);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private void SetupModeratorAuth()
    {
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(Guid.NewGuid(), "moderator@test.com", "Moderator"));
    }

    private static Route CreateRoute(Guid routeId)
    {
        return new Route(
            id: routeId,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            name: "Existing Route",
            vehicleType: VehicleType.Jeepney,
            status: RouteStatus.Unverified,
            createdBy: Guid.NewGuid(),
            baseFare: 13.0m,
            waypoints: new List<Waypoint>
            {
                new(14.5547, 121.0244, 0, "Ayala"),
                new(14.5870, 121.0560, 1, "Guadalupe")
            });
    }

    private static RouteRevision CreateRevision(Guid revisionId, Guid routeId, RevisionStatus status)
    {
        return new RouteRevision(
            id: revisionId,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            routeId: routeId,
            submittedBy: Guid.NewGuid(),
            status: status,
            waypoints: new List<Waypoint>
            {
                new(14.6000, 121.0300, 0, "New Start"),
                new(14.6500, 121.0600, 1, "New End")
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
        public List<Route> UpdatedRoutes { get; } = new();

        public void SetRouteToReturn(Route route) => _routeToReturn = route;

        public Task<Route?> FindByIdWithWaypointsAsync(Guid id) => Task.FromResult(_routeToReturn);
        public Task<Route?> FindByIdAsync(Guid id) => Task.FromResult(_routeToReturn);

        public Task<Route> UpdateAsync(Route entity)
        {
            UpdatedRoutes.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<Route> CreateAsync(Route entity) => Task.FromResult(entity);
        public Task<IReadOnlyList<Route>> FindAllAsync() => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<IReadOnlyList<Route>> WhereAsync(Expression<Func<Route, bool>> predicate) => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
        public Task<Route> SaveAsync(Route entity) => Task.FromResult(entity);
        public Task DeleteAsync(Route entity) => Task.CompletedTask;
        public Task<IReadOnlyList<Route>> FindByBboxAsync(double minLat, double minLon, double maxLat, double maxLon) => Task.FromResult<IReadOnlyList<Route>>(new List<Route>());
    }

    private sealed class FakeRouteRevisionRepository : IRouteRevisionRepository
    {
        private RouteRevision? _revisionToReturn;
        public List<RouteRevision> UpdatedRevisions { get; } = new();

        public void SetRevisionToReturn(RouteRevision revision) => _revisionToReturn = revision;

        public Task<RouteRevision?> FindByIdAsync(Guid id) => Task.FromResult(_revisionToReturn);

        public Task<RouteRevision> UpdateAsync(RouteRevision entity)
        {
            UpdatedRevisions.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<RouteRevision> CreateAsync(RouteRevision entity) => Task.FromResult(entity);
        public Task<IReadOnlyList<RouteRevision>> FindAllAsync() => Task.FromResult<IReadOnlyList<RouteRevision>>(new List<RouteRevision>());
        public Task<IReadOnlyList<RouteRevision>> WhereAsync(Expression<Func<RouteRevision, bool>> predicate) => Task.FromResult<IReadOnlyList<RouteRevision>>(new List<RouteRevision>());
        public Task<RouteRevision> SaveAsync(RouteRevision entity) => Task.FromResult(entity);
        public Task DeleteAsync(RouteRevision entity) => Task.CompletedTask;
        public Task<IReadOnlyList<RouteRevision>> FindByRouteIdAsync(Guid routeId) => Task.FromResult<IReadOnlyList<RouteRevision>>(new List<RouteRevision>());
    }
}
