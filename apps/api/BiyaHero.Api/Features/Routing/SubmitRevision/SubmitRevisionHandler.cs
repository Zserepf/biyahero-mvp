using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Routing.SubmitRevision;

/// <summary>
/// Business logic for submitting a route revision (POST /v1/routes/{id}/revisions).
/// Authenticates the caller, verifies the route exists, validates waypoints,
/// creates a pending revision linked to the original route without overwriting it.
/// Requirements: 1.3, 1.6
/// </summary>
public sealed class SubmitRevisionHandler
{
    // Philippines bounding box per Req 1.8
    private const double MinLatitude = 4.5;
    private const double MaxLatitude = 21.5;
    private const double MinLongitude = 116.0;
    private const double MaxLongitude = 127.0;

    private readonly IRouteRepository _routeRepository;
    private readonly IRouteRevisionRepository _revisionRepository;
    private readonly IJwtService _jwtService;

    public SubmitRevisionHandler(
        IRouteRepository routeRepository,
        IRouteRevisionRepository revisionRepository,
        IJwtService jwtService)
    {
        _routeRepository = routeRepository;
        _revisionRepository = revisionRepository;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Submits a pending revision for an existing route.
    /// Throws UnauthenticatedException for missing/invalid JWT (401).
    /// Throws NotFoundException if the route does not exist (404).
    /// Throws SubmitRevisionValidationException for invalid input (422).
    /// </summary>
    public async Task<SubmitRevisionResponse> HandleAsync(
        Guid routeId,
        string? authorizationHeader,
        SubmitRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Authenticate — extract user ID from JWT
        var userId = await AuthenticateAsync(authorizationHeader, cancellationToken);

        // Verify the route exists
        var route = await _routeRepository.FindByIdAsync(routeId);
        if (route is null)
        {
            throw new NotFoundException($"Route with id '{routeId}' not found.");
        }

        // Validate input
        Validate(request);

        // Build domain waypoints from DTOs
        var waypoints = request.Waypoints
            .Select(w => new Waypoint(w.Latitude, w.Longitude, w.SequenceOrder, w.Label))
            .ToList();

        // Create revision domain object with status Pending
        var now = DateTime.UtcNow;
        var revision = new RouteRevision(
            id: Guid.NewGuid(),
            createdAt: now,
            updatedAt: now,
            routeId: routeId,
            submittedBy: userId,
            status: RevisionStatus.Pending,
            waypoints: waypoints);

        // Persist via repository
        await _revisionRepository.CreateAsync(revision);

        // Return 201 response
        return new SubmitRevisionResponse(
            RevisionId: revision.Id,
            RouteId: revision.RouteId,
            Status: revision.Status.ToString(),
            WaypointCount: revision.Waypoints.Count);
    }

    private async Task<Guid> AuthenticateAsync(string? authorizationHeader, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthenticatedException("Missing or invalid Authorization header.");
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();

        var validationResult = await _jwtService.ValidateTokenDetailedAsync(token, ct);
        if (!validationResult.IsValid)
        {
            throw new UnauthenticatedException(validationResult.ErrorMessage ?? "Invalid token.");
        }

        return validationResult.UserId!.Value;
    }

    private static void Validate(SubmitRevisionRequest request)
    {
        // At least 2 waypoints required
        if (request.Waypoints is null || request.Waypoints.Count < 2)
        {
            throw new SubmitRevisionValidationException("A revision must have at least 2 waypoints.");
        }

        // Validate each waypoint is within Philippines bounding box
        for (var i = 0; i < request.Waypoints.Count; i++)
        {
            var wp = request.Waypoints[i];

            if (wp.Latitude < MinLatitude || wp.Latitude > MaxLatitude)
            {
                throw new SubmitRevisionValidationException(
                    $"Waypoint {i} latitude {wp.Latitude} is outside the valid range ({MinLatitude}° to {MaxLatitude}° N).");
            }

            if (wp.Longitude < MinLongitude || wp.Longitude > MaxLongitude)
            {
                throw new SubmitRevisionValidationException(
                    $"Waypoint {i} longitude {wp.Longitude} is outside the valid range ({MinLongitude}° to {MaxLongitude}° E).");
            }
        }
    }
}
