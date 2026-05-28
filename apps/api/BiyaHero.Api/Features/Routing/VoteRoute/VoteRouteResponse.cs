namespace BiyaHero.Api.Features.Routing.VoteRoute;

/// <summary>
/// Response body for a successfully cast route vote.
/// Returns the vote ID, route ID, vote kind, and a confirmation message.
/// </summary>
public sealed record VoteRouteResponse(
    Guid VoteId,
    Guid RouteId,
    string Kind,
    string Message);
