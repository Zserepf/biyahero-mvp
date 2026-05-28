using System.Collections.Concurrent;
using System.Linq.Expressions;
using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// In-memory user repository for local development without PostgreSQL.
/// Data is lost on restart — suitable for demos and development only.
/// </summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public Task<User?> FindByIdAsync(Guid id)
    {
        _users.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> FindByEmailAsync(string email)
    {
        var user = _users.Values.FirstOrDefault(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public Task<bool> EmailExistsAsync(string email)
    {
        var exists = _users.Values.Any(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<User>> FindAllAsync()
    {
        return Task.FromResult<IReadOnlyList<User>>(_users.Values.ToList());
    }

    public Task<User> CreateAsync(User entity)
    {
        if (entity.Id == Guid.Empty)
            entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _users[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<User>> WhereAsync(Expression<Func<User, bool>> predicate)
    {
        var compiled = predicate.Compile();
        var results = _users.Values.Where(compiled).ToList();
        return Task.FromResult<IReadOnlyList<User>>(results);
    }

    public Task<User> SaveAsync(User entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _users[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<User> UpdateAsync(User entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _users[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(User entity)
    {
        _users.TryRemove(entity.Id, out _);
        return Task.CompletedTask;
    }

    public Task<User> UpdateLanguagePreferenceAsync(Guid userId, string languagePreference)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            user.LanguagePreference = languagePreference;
            user.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(user);
        }
        throw new InvalidOperationException("User not found.");
    }

    public Task<User> SuspendAsync(Guid userId)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            user.Status = UserStatus.Suspended;
            user.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(user);
        }
        throw new InvalidOperationException("User not found.");
    }

    public Task<User> ChangeRoleAsync(Guid userId, UserRole newRole)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            user.Role = newRole;
            user.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(user);
        }
        throw new InvalidOperationException("User not found.");
    }
}
