using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Routing.ApproveRevision;

/// <summary>
/// Business logic for approving a pending route revision (POST /v1/routes/{id}/revisions/{rid}/:approve).
/// Authenticates the caller, verifies Moderator or SuperAdmin role, validates the route and revision exist,
/// ensures the revision is in Pending status, then applies the revision's waypoints to the route
/// and sets the route status to Verified.
/// Requirements: 1.4, 5.8
/// </summary>
public sealed class ApproveRevisionHandler
{
    private readonly IRouteRepository _routeRepository;
    private readonly IRouteRevisionRepository _revisionRepository;
    private readonly IJwtService _jwtService;

    public ApproveRevisionHandler(
        IRouteRepository routeRepository,
        IRouteRevisionRepository revisionRepository,
        IJwtService jwtService)
    {
        _routeRepository = routeRepository;
        _revisionRepository = revisionRepository;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Approves a pending revision, applying its waypoints to the parent route.
    /// Throws UnauthenticatedException for missing/invalid JWT (401).
    /// Throws ForbiddenException if user is not Moderator or SuperAdmin (403).
    /// Throws NotFoundException if route or revision does not exist (404).
    /// Throws ConflictException if revision is not in Pending status (409).
    /// </summary>
    public async Task<ApproveRevisionResponse> HandleAsync(
        string? authorizationHeader,
        Guid routeId,
        Guid revisionId,
        CancellationToken cancellationToken = default)
    {
        // 1. Authenticate — extract user identity and role from JWT
        var validationResult = await AuthenticateAsync(authorizationHeader, cancellationToken);

        // 2. Authorize — only Moderator or SuperAdmin can approve revisions
        AuthorizeModeratorOrSuperAdmin(validationResult.Role);

        // 3. Verify the route exists
        var route = await _routeRepository.FindByIdWithWaypointsAsync(routeId);
        if (route is null)
        {
            throw new NotFoundException($"Route with ID '{routeId}' not found.");
        }

        // 4. Verify the revision exists and belongs to this route
        var revision = await _revisionRepository.FindByIdAsync(revisionId);
        if (revision is null || revision.RouteId != routeId)
        {
            throw new NotFoundException($"Revision with ID '{revisionId}' not found for route '{routeId}'.");
        }

        // 5. Verify the revision is in Pending status
        if (revision.Status != RevisionStatus.Pending)
        {
            throw new ConflictException(
                $"Revision '{revisionId}' is in '{revision.Status}' status and cannot be approved. Only Pending revisions can be approved.");
        }

        // 6. Apply the revision: replace route waypoints with revision waypoints, set route to Verified
        route.Waypoints = revision.Waypoints;
        route.Status = RouteStatus.Verified;
        route.UpdatedAt = DateTime.UtcNow;
        await _routeRepository.UpdateAsync(route);

        // 7. Mark revision as Approved and record the approver
        revision.Status = RevisionStatus.Approved;
        revision.ApproverId = validationResult.UserId;
        revision.UpdatedAt = DateTime.UtcNow;
        await _revisionRepository.UpdateAsync(revision);

        // 8. Return success response with updated route info
        var routeDto = new ApproveRevisionRouteDto(
            Id: route.Id,
            Name: route.Name,
            VehicleType: route.VehicleType.ToString(),
            Status: route.Status.ToString(),
            BaseFare: route.BaseFare,
            CreatedBy: route.CreatedBy,
            CreatedAt: route.CreatedAt,
            UpdatedAt: route.UpdatedAt,
            Waypoints: route.Waypoints
                .OrderBy(w => w.SequenceOrder)
                .Select(w => new ApproveRevisionWaypointDto(
                    Latitude: w.Latitude,
                    Longitude: w.Longitude,
                    SequenceOrder: w.SequenceOrder,
                    Label: w.Label))
                .ToList());

        return new ApproveRevisionResponse(
            RevisionId: revisionId,
            RouteId: routeId,
            RevisionStatus: "Approved",
            Route: routeDto);
    }

    private async Task<JwtValidationResult> AuthenticateAsync(string? authorizationHeader, CancellationToken ct)
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

        return validationResult;
    }

    private static void AuthorizeModeratorOrSuperAdmin(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ForbiddenException("Insufficient permissions. Moderator or SuperAdmin role required.");
        }

        var isAuthorized = string.Equals(role, nameof(UserRole.Moderator), StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase);

        if (!isAuthorized)
        {
            throw new ForbiddenException("Insufficient permissions. Moderator or SuperAdmin role required.");
        }
    }
}
