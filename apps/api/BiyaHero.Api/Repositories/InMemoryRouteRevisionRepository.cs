using System.Collections.Concurrent;
using System.Linq.Expressions;
using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// In-memory implementation of IRouteRevisionRepository for MVP.
/// Stores route revisions in a thread-safe concurrent dictionary.
/// Can be replaced with a PostgreSQL-backed implementation post-MVP.
/// </summary>
public sealed class InMemoryRouteRevisionRepository : IRouteRevisionRepository
{
    private readonly ConcurrentDictionary<Guid, RouteRevision> _store = new();

    public Task<RouteRevision?> FindByIdAsync(Guid id)
    {
        _store.TryGetValue(id, out var revision);
        return Task.FromResult(revision);
    }

    public Task<IReadOnlyList<RouteRevision>> FindAllAsync()
    {
        IReadOnlyList<RouteRevision> result = _store.Values.ToList();
        return Task.FromResult(result);
    }

    public Task<RouteRevision> CreateAsync(RouteRevision entity)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<RouteRevision>> WhereAsync(Expression<Func<RouteRevision, bool>> predicate)
    {
        var compiled = predicate.Compile();
        IReadOnlyList<RouteRevision> result = _store.Values.Where(compiled).ToList();
        return Task.FromResult(result);
    }

    public Task<RouteRevision> SaveAsync(RouteRevision entity)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<RouteRevision> UpdateAsync(RouteRevision entity)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(RouteRevision entity)
    {
        _store.TryRemove(entity.Id, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RouteRevision>> FindByRouteIdAsync(Guid routeId)
    {
        IReadOnlyList<RouteRevision> result = _store.Values
            .Where(r => r.RouteId == routeId)
            .ToList();
        return Task.FromResult(result);
    }
}
