using System.Linq.Expressions;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Tests.Domain;

namespace BiyaHero.Api.Tests.Repositories;

/// <summary>
/// In-memory implementation of IRepository for testing purposes.
/// Allows unit tests to verify repository CRUD operations without
/// requiring a real Postgres or DynamoDB connection.
/// </summary>
public class InMemoryRepository<T> : IRepository<T> where T : BaseDomain
{
    private readonly Dictionary<Guid, T> _store = new();

    public Task<T?> FindByIdAsync(Guid id)
    {
        _store.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<T>> FindAllAsync()
    {
        IReadOnlyList<T> result = _store.Values.ToList().AsReadOnly();
        return Task.FromResult(result);
    }

    public Task<T> CreateAsync(T entity)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<T>> WhereAsync(Expression<Func<T, bool>> predicate)
    {
        var compiled = predicate.Compile();
        IReadOnlyList<T> result = _store.Values.Where(compiled).ToList().AsReadOnly();
        return Task.FromResult(result);
    }

    public Task<T> SaveAsync(T entity)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<T> UpdateAsync(T entity)
    {
        if (!_store.ContainsKey(entity.Id))
            throw new InvalidOperationException($"Entity with ID {entity.Id} not found for update.");
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(T entity)
    {
        _store.Remove(entity.Id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Helper to check the current count of stored entities.
    /// </summary>
    public int Count => _store.Count;
}

/// <summary>
/// Unit tests for base repository CRUD operations using the in-memory stub.
/// Validates: Requirements 1.11, 3.11
/// Verifies that BaseDomain static/instance methods work correctly
/// with the repository abstraction.
/// </summary>
public class InMemoryRepositoryTests
{
    private readonly InMemoryRepository<TestEntity> _repository;

    public InMemoryRepositoryTests()
    {
        _repository = new InMemoryRepository<TestEntity>();
    }

    [Fact]
    public async Task Create_PersistsEntity_AndReturnsIt()
    {
        // Arrange
        var entity = new TestEntity
        {
            Name = "Jeepney Route A",
            Amount = 12.00m,
            Latitude = 14.5995,
            Longitude = 120.9842
        };

        // Act
        var created = await BaseDomain.Create(_repository, entity);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(entity.Id, created.Id);
        Assert.Equal("Jeepney Route A", created.Name);
        Assert.Equal(1, _repository.Count);
    }

    [Fact]
    public async Task Find_ReturnsEntity_WhenExists()
    {
        // Arrange
        var entity = new TestEntity { Name = "Route B", Amount = 15.00m };
        await BaseDomain.Create(_repository, entity);

        // Act
        var found = await BaseDomain.Find(_repository, entity.Id);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(entity.Id, found.Id);
        Assert.Equal("Route B", found.Name);
    }

    [Fact]
    public async Task Find_ReturnsNull_WhenNotExists()
    {
        // Act
        var found = await BaseDomain.Find(_repository, Guid.NewGuid());

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public async Task FindAll_ReturnsAllEntities()
    {
        // Arrange
        await BaseDomain.Create(_repository, new TestEntity { Name = "Route 1" });
        await BaseDomain.Create(_repository, new TestEntity { Name = "Route 2" });
        await BaseDomain.Create(_repository, new TestEntity { Name = "Route 3" });

        // Act
        var all = await BaseDomain.FindAll(_repository);

        // Assert
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task Where_FiltersEntitiesByPredicate()
    {
        // Arrange
        await BaseDomain.Create(_repository, new TestEntity { Name = "Manila", Amount = 10.00m });
        await BaseDomain.Create(_repository, new TestEntity { Name = "Cubao", Amount = 15.00m });
        await BaseDomain.Create(_repository, new TestEntity { Name = "Manila Express", Amount = 20.00m });

        // Act
        var manilaRoutes = await BaseDomain.Where<TestEntity>(_repository, e => e.Name.Contains("Manila"));

        // Assert
        Assert.Equal(2, manilaRoutes.Count);
        Assert.All(manilaRoutes, e => Assert.Contains("Manila", e.Name));
    }

    [Fact]
    public async Task Save_PersistsEntityState()
    {
        // Arrange
        var entity = new TestEntity { Name = "Original Name", Amount = 10.00m };
        await BaseDomain.Create(_repository, entity);

        // Act
        entity.Name = "Updated Name";
        await entity.Save(_repository);

        // Assert
        var found = await BaseDomain.Find(_repository, entity.Id);
        Assert.NotNull(found);
        Assert.Equal("Updated Name", found.Name);
    }

    [Fact]
    public async Task Update_ModifiesExistingEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "Before Update", Amount = 5.00m };
        await BaseDomain.Create(_repository, entity);

        // Act
        entity.Name = "After Update";
        entity.Amount = 25.00m;
        await entity.Update(_repository);

        // Assert
        var found = await BaseDomain.Find(_repository, entity.Id);
        Assert.NotNull(found);
        Assert.Equal("After Update", found.Name);
        Assert.Equal(25.00m, found.Amount);
    }

    [Fact]
    public async Task Update_ThrowsWhenEntityNotFound()
    {
        // Arrange
        var entity = new TestEntity { Name = "Ghost Entity" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => entity.Update(_repository));
    }

    [Fact]
    public async Task Delete_RemovesEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "To Delete" };
        await BaseDomain.Create(_repository, entity);
        Assert.Equal(1, _repository.Count);

        // Act
        await entity.Delete(_repository);

        // Assert
        Assert.Equal(0, _repository.Count);
        var found = await BaseDomain.Find(_repository, entity.Id);
        Assert.Null(found);
    }

    [Fact]
    public async Task Create_SetsTimestamps()
    {
        // Arrange
        var entity = new TestEntity { Name = "Timestamped" };
        var beforeCreate = DateTime.UtcNow;

        // Act
        var created = await BaseDomain.Create(_repository, entity);

        // Assert
        Assert.True(created.CreatedAt >= beforeCreate);
        Assert.True(created.UpdatedAt >= beforeCreate);
    }

    [Fact]
    public async Task Save_UpdatesTimestamp()
    {
        // Arrange
        var entity = new TestEntity { Name = "Timestamp Test" };
        await BaseDomain.Create(_repository, entity);
        var originalUpdatedAt = entity.UpdatedAt;

        // Small delay to ensure timestamp difference
        await Task.Delay(10);

        // Act
        entity.Name = "Modified";
        await entity.Save(_repository);

        // Assert
        Assert.True(entity.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public async Task RoundTrip_CreateSerializeParse_ProducesEquivalentEntity()
    {
        // Arrange: Create and persist an entity
        var original = new TestEntity
        {
            Name = "Round Trip Route",
            Amount = 18.50m,
            Latitude = 14.6507,
            Longitude = 121.0495
        };
        await BaseDomain.Create(_repository, original);

        // Act: Retrieve, serialize, parse
        var retrieved = await BaseDomain.Find(_repository, original.Id);
        Assert.NotNull(retrieved);

        var serialized = retrieved.Serialize();
        var parsed = TestEntity.Parse(serialized);

        // Assert: Parsed entity is equivalent to the original
        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.Name, parsed.Name);
        Assert.Equal(original.Amount, parsed.Amount);
        Assert.Equal(original.Latitude, parsed.Latitude);
        Assert.Equal(original.Longitude, parsed.Longitude);
    }
}
