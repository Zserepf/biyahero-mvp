using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Domain;

/// <summary>
/// Abstract base class for all domain entities.
/// Provides standard OOP interface per the steering:
///   Class methods: Find, FindAll, Create, Where
///   Instance methods: Save, Update, Delete, Serialize
/// Every entity carries Id, CreatedAt, UpdatedAt.
/// Persistence is delegated to IRepository&lt;T&gt; — domain never touches data stores directly.
/// </summary>
public abstract class BaseDomain
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    protected BaseDomain()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    protected BaseDomain(Guid id, DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    // ─── Static / Class Methods ───────────────────────────────────────────

    /// <summary>
    /// Find a single entity by its ID. Returns null if not found.
    /// </summary>
    public static async Task<T?> Find<T>(IRepository<T> repository, Guid id) where T : BaseDomain
    {
        return await repository.FindByIdAsync(id);
    }

    /// <summary>
    /// Return all entities of this type.
    /// </summary>
    public static async Task<IReadOnlyList<T>> FindAll<T>(IRepository<T> repository) where T : BaseDomain
    {
        return await repository.FindAllAsync();
    }

    /// <summary>
    /// Persist a new entity and return the created instance.
    /// </summary>
    public static async Task<T> Create<T>(IRepository<T> repository, T entity) where T : BaseDomain
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        return await repository.CreateAsync(entity);
    }

    /// <summary>
    /// Return entities matching the given predicate.
    /// </summary>
    public static async Task<IReadOnlyList<T>> Where<T>(
        IRepository<T> repository,
        Expression<Func<T, bool>> predicate) where T : BaseDomain
    {
        return await repository.WhereAsync(predicate);
    }

    // ─── Instance Methods ─────────────────────────────────────────────────

    /// <summary>
    /// Persist the current state of this entity (insert if new).
    /// </summary>
    public async Task<T> Save<T>(IRepository<T> repository) where T : BaseDomain
    {
        UpdatedAt = DateTime.UtcNow;
        return await repository.SaveAsync((T)this);
    }

    /// <summary>
    /// Persist changes to an existing entity.
    /// </summary>
    public async Task<T> Update<T>(IRepository<T> repository) where T : BaseDomain
    {
        UpdatedAt = DateTime.UtcNow;
        return await repository.UpdateAsync((T)this);
    }

    /// <summary>
    /// Remove this entity from the data store.
    /// </summary>
    public async Task Delete<T>(IRepository<T> repository) where T : BaseDomain
    {
        await repository.DeleteAsync((T)this);
    }

    /// <summary>
    /// Serialize this entity to a JSON-compatible dictionary.
    /// Subclasses override to include their own properties.
    /// Uses System.Text.Json for AOT compatibility.
    /// </summary>
    public virtual Dictionary<string, object?> Serialize()
    {
        return new Dictionary<string, object?>
        {
            ["id"] = Id.ToString(),
            ["createdAt"] = CreatedAt.ToString("o"),
            ["updatedAt"] = UpdatedAt.ToString("o")
        };
    }

    /// <summary>
    /// Serialize this entity to a JSON string using the provided JsonSerializerOptions.
    /// AOT-compatible when used with a source-generated JsonSerializerContext.
    /// </summary>
    public string SerializeToJson(JsonSerializerOptions? options = null)
    {
        var dict = Serialize();
        if (options != null)
            return JsonSerializer.Serialize(dict, options);
        return JsonSerializer.Serialize(dict, BaseDomainJsonContext.Default.DictionaryStringObject);
    }
}

/// <summary>
/// AOT-compatible JSON serializer context for BaseDomain serialization.
/// Subclasses should define their own contexts for type-specific serialization.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTime))]
public partial class BaseDomainJsonContext : JsonSerializerContext
{
}
