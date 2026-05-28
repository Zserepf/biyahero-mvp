using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Repository interface for RouteRevision-specific data access.
/// Extends the generic IRepository with revision-specific queries.
/// </summary>
public interface IRouteRevisionRepository : IRepository<RouteRevision>
{
    /// <summary>
    /// Finds all pending revisions for a given route.
    /// </summary>
    Task<IReadOnlyList<RouteRevision>> FindByRouteIdAsync(Guid routeId);
}
