namespace BiyaHero.Api.Features.Routing.VoteRoute;

/// <summary>
/// Request body for POST /v1/routes/{id}/votes.
/// Contains the vote kind: "still_accurate" or "no_longer_accurate".
/// </summary>
public sealed record VoteRouteRequest(string Kind);
