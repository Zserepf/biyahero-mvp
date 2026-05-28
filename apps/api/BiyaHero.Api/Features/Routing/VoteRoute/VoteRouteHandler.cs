using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Routing.VoteRoute;

/// <summary>
/// Business logic for casting an accuracy vote on a route (POST /v1/routes/{id}/votes).
/// Authenticates the caller, verifies the route exists, validates the vote kind,
/// and enforces one vote per user per route (returns 409 on duplicate).
/// Requirements: 1.5, 1.6
/// </summary>
public sealed class VoteRouteHandler
{
    private static readonly Dictionary<string, VoteKind> ValidVoteKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["still_accurate"] = VoteKind.StillAccurate,
        ["no_longer_accurate"] = VoteKind.NoLongerAccurate
    };

    private readonly IRouteRepository _routeRepository;
    private readonly IRouteVoteRepository _routeVoteRepository;
    private readonly IJwtService _jwtService;

    public VoteRouteHandler(
        IRouteRepository routeRepository,
        IRouteVoteRepository routeVoteRepository,
        IJwtService jwtService)
    {
        _routeRepository = routeRepository;
        _routeVoteRepository = routeVoteRepository;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Casts an accuracy vote on a route.
    /// Throws UnauthenticatedException for missing/invalid JWT (401).
    /// Returns null if the route does not exist (caller handles 404).
    /// Throws VoteRouteValidationException for invalid vote kind (422).
    /// Throws ConflictException if user already voted on this route (409).
    /// </summary>
    public async Task<VoteRouteResponse?> HandleAsync(
        Guid routeId,
        string? authorizationHeader,
        VoteRouteRequest request,
        CancellationToken cancellationToken = default)
    {
        // Authenticate — extract user ID from JWT
        var userId = await AuthenticateAsync(authorizationHeader, cancellationToken);

        // Verify route exists
        var route = await _routeRepository.FindByIdWithWaypointsAsync(routeId);
        if (route is null)
        {
            return null; // Endpoint will return 404
        }

        // Validate vote kind (accepts snake_case: "still_accurate" or "no_longer_accurate")
        if (string.IsNullOrWhiteSpace(request.Kind) || !ValidVoteKinds.TryGetValue(request.Kind, out var voteKind))
        {
            throw new VoteRouteValidationException(
                $"Invalid vote kind '{request.Kind}'. Must be one of: still_accurate, no_longer_accurate.");
        }

        // Enforce one vote per user per route — return 409 on duplicate
        var alreadyVoted = await _routeVoteRepository.ExistsAsync(routeId, userId);
        if (alreadyVoted)
        {
            throw new ConflictException(
                $"User has already voted on route '{routeId}'. Only one vote per user per route is allowed.");
        }

        // Persist the vote
        var now = DateTime.UtcNow;
        var vote = new RouteVote(
            id: Guid.NewGuid(),
            createdAt: now,
            updatedAt: now,
            routeId: routeId,
            voterId: userId,
            kind: voteKind,
            timestamp: now);

        await _routeVoteRepository.CreateAsync(vote);

        return new VoteRouteResponse(
            VoteId: vote.Id,
            RouteId: vote.RouteId,
            Kind: request.Kind,
            Message: "Vote recorded successfully.");
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
}
