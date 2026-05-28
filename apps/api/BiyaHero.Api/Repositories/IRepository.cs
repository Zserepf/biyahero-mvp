using System.Linq.Expressions;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Generic repository interface that abstracts data persistence.
/// Domain classes delegate all data operations through this contract
/// so handlers never touch SQL or SDK calls directly.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> FindByIdAsync(Guid id);
    Task<IReadOnlyList<T>> FindAllAsync();
    Task<T> CreateAsync(T entity);
    Task<IReadOnlyList<T>> WhereAsync(Expression<Func<T, bool>> predicate);
    Task<T> SaveAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
