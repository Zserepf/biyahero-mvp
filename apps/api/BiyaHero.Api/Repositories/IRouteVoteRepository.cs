using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Repository interface for RouteVote-specific data access.
/// Extends the generic IRepository with duplicate-check and route-scoped queries.
/// </summary>
public interface IRouteVoteRepository : IRepository<RouteVote>
{
    /// <summary>
    /// Checks whether a vote already exists for the given route and voter combination.
    /// Used to enforce the one-vote-per-user-per-route constraint before insert.
    /// </summary>
    Task<bool> ExistsAsync(Guid routeId, Guid voterId);
}
