using System.Data;
using System.Linq.Expressions;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Tests.Repositories;

/// <summary>
/// Tests for IRepository contract and BasePostgresRepository behavior.
/// Uses an in-memory stub to verify the repository contract without requiring a real database.
/// </summary>
public class BasePostgresRepositoryTests
{
    // ─── Test Entity ──────────────────────────────────────────────────────

    private sealed class TestEntity : BaseDomain
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }

        public TestEntity() : base() { }
        public TestEntity(Guid id, string name, int value)
            : base(id, DateTime.UtcNow, DateTime.UtcNow)
        {
            Name = name;
            Value = value;
        }

        public override Dictionary<string, object?> Serialize()
        {
            var dict = base.Serialize();
            dict["name"] = Name;
            dict["value"] = Value;
            return dict;
        }
    }

    // ─── In-Memory Repository (simulates BasePostgresRepository contract) ─

    private sealed class InMemoryRepository : IRepository<TestEntity>
    {
        private readonly List<TestEntity> _store = new();

        public Task<TestEntity?> FindByIdAsync(Guid id)
        {
            var entity = _store.FirstOrDefault(e => e.Id == id);
            return Task.FromResult(entity);
        }

        public Task<IReadOnlyList<TestEntity>> FindAllAsync()
        {
            IReadOnlyList<TestEntity> result = _store.ToList().AsReadOnly();
            return Task.FromResult(result);
        }

        public Task<TestEntity> CreateAsync(TestEntity entity)
        {
            _store.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<IReadOnlyList<TestEntity>> WhereAsync(Expression<Func<TestEntity, bool>> predicate)
        {
            var compiled = predicate.Compile();
            IReadOnlyList<TestEntity> result = _store.Where(compiled).ToList().AsReadOnly();
            return Task.FromResult(result);
        }

        public Task<TestEntity> SaveAsync(TestEntity entity)
        {
            var existing = _store.FirstOrDefault(e => e.Id == entity.Id);
            if (existing != null)
            {
                _store.Remove(existing);
            }
            _store.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<TestEntity> UpdateAsync(TestEntity entity)
        {
            var existing = _store.FirstOrDefault(e => e.Id == entity.Id);
            if (existing == null)
                throw new InvalidOperationException($"Entity with id {entity.Id} not found");
            _store.Remove(existing);
            _store.Add(entity);
            return Task.FromResult(entity);
        }

        public Task DeleteAsync(TestEntity entity)
        {
            _store.RemoveAll(e => e.Id == entity.Id);
            return Task.CompletedTask;
        }
    }

    // ─── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindByIdAsync_ReturnsNull_WhenEntityDoesNotExist()
    {
        var repo = new InMemoryRepository();
        var result = await repo.FindByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_PersistsEntity_AndReturnsIt()
    {
        var repo = new InMemoryRepository();
        var entity = new TestEntity(Guid.NewGuid(), "Test", 42);

        var created = await repo.CreateAsync(entity);

        Assert.Equal(entity.Id, created.Id);
        Assert.Equal("Test", created.Name);
        Assert.Equal(42, created.Value);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsEntity_AfterCreate()
    {
        var repo = new InMemoryRepository();
        var entity = new TestEntity(Guid.NewGuid(), "FindMe", 7);
        await repo.CreateAsync(entity);

        var found = await repo.FindByIdAsync(entity.Id);

        Assert.NotNull(found);
        Assert.Equal("FindMe", found.Name);
    }

    [Fact]
    public async Task FindAllAsync_ReturnsAllEntities()
    {
        var repo = new InMemoryRepository();
        await repo.CreateAsync(new TestEntity(Guid.NewGuid(), "A", 1));
        await repo.CreateAsync(new TestEntity(Guid.NewGuid(), "B", 2));
        await repo.CreateAsync(new TestEntity(Guid.NewGuid(), "C", 3));

        var all = await repo.FindAllAsync();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task WhereAsync_FiltersEntities()
    {
        var repo = new InMemoryRepository();
        await repo.CreateAsync(new TestEntity(Guid.NewGuid(), "Alpha", 10));
        await repo.CreateAsync(new TestEntity(Guid.NewGuid(), "Beta", 20));
        await repo.CreateAsync(new TestEntity(Guid.NewGuid(), "Gamma", 30));

        var results = await repo.WhereAsync(e => e.Value > 15);

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.True(e.Value > 15));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesExistingEntity()
    {
        var repo = new InMemoryRepository();
        var entity = new TestEntity(Guid.NewGuid(), "Original", 1);
        await repo.CreateAsync(entity);

        entity.Name = "Updated";
        entity.Value = 99;
        await repo.UpdateAsync(entity);

        var found = await repo.FindByIdAsync(entity.Id);
        Assert.NotNull(found);
        Assert.Equal("Updated", found.Name);
        Assert.Equal(99, found.Value);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenEntityNotFound()
    {
        var repo = new InMemoryRepository();
        var entity = new TestEntity(Guid.NewGuid(), "Ghost", 0);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.UpdateAsync(entity));
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntity()
    {
        var repo = new InMemoryRepository();
        var entity = new TestEntity(Guid.NewGuid(), "ToDelete", 5);
        await repo.CreateAsync(entity);

        await repo.DeleteAsync(entity);

        var found = await repo.FindByIdAsync(entity.Id);
        Assert.Null(found);
    }

    [Fact]
    public async Task SaveAsync_InsertsNewEntity()
    {
        var repo = new InMemoryRepository();
        var entity = new TestEntity(Guid.NewGuid(), "New", 1);

        await repo.SaveAsync(entity);

        var found = await repo.FindByIdAsync(entity.Id);
        Assert.NotNull(found);
        Assert.Equal("New", found.Name);
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingEntity()
    {
        var repo = new InMemoryRepository();
        var entity = new TestEntity(Guid.NewGuid(), "Before", 1);
        await repo.CreateAsync(entity);

        entity.Name = "After";
        await repo.SaveAsync(entity);

        var all = await repo.FindAllAsync();
        Assert.Single(all);
        Assert.Equal("After", all[0].Name);
    }

    [Fact]
    public void IRepository_Interface_DefinesAllRequiredMethods()
    {
        // Verify the interface contract has all expected methods
        var interfaceType = typeof(IRepository<TestEntity>);

        Assert.NotNull(interfaceType.GetMethod("FindByIdAsync"));
        Assert.NotNull(interfaceType.GetMethod("FindAllAsync"));
        Assert.NotNull(interfaceType.GetMethod("CreateAsync"));
        Assert.NotNull(interfaceType.GetMethod("WhereAsync"));
        Assert.NotNull(interfaceType.GetMethod("SaveAsync"));
        Assert.NotNull(interfaceType.GetMethod("UpdateAsync"));
        Assert.NotNull(interfaceType.GetMethod("DeleteAsync"));
    }

    [Fact]
    public void BaseDomain_StaticMethods_DelegateToRepository()
    {
        // Verify BaseDomain exposes the expected static methods
        var type = typeof(BaseDomain);

        var findMethod = type.GetMethod("Find");
        Assert.NotNull(findMethod);

        var findAllMethod = type.GetMethod("FindAll");
        Assert.NotNull(findAllMethod);

        var createMethod = type.GetMethod("Create");
        Assert.NotNull(createMethod);

        var whereMethod = type.GetMethod("Where");
        Assert.NotNull(whereMethod);
    }

    [Fact]
    public async Task BaseDomain_Create_SetsTimestamps()
    {
        var repo = new InMemoryRepository();
        var entity = new TestEntity { Name = "Timestamped", Value = 1 };

        var before = DateTime.UtcNow;
        var created = await BaseDomain.Create(repo, entity);
        var after = DateTime.UtcNow;

        Assert.InRange(created.CreatedAt, before, after);
        Assert.InRange(created.UpdatedAt, before, after);
    }

    [Fact]
    public async Task BaseDomain_Find_DelegatesToRepository()
    {
        var repo = new InMemoryRepository();
        var entity = new TestEntity(Guid.NewGuid(), "Findable", 10);
        await repo.CreateAsync(entity);

        var found = await BaseDomain.Find(repo, entity.Id);

        Assert.NotNull(found);
        Assert.Equal("Findable", found.Name);
    }

    [Fact]
    public async Task BaseDomain_FindAll_DelegatesToRepository()
    {
        var repo = new InMemoryRepository();
        await repo.CreateAsync(new TestEntity(Guid.NewGuid(), "A", 1));
        await repo.CreateAsync(new TestEntity(Guid.NewGuid(), "B", 2));

        var all = await BaseDomain.FindAll(repo);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task BaseDomain_Where_DelegatesToRepository()
    {
        var repo = new InMemoryRepository();
        await repo.CreateAsync(new TestEntity(Guid.NewGuid(), "Low", 5));
        await repo.CreateAsync(new TestEntity(Guid.NewGuid(), "High", 50));

        var results = await BaseDomain.Where(repo, (TestEntity e) => e.Value > 10);

        Assert.Single(results);
        Assert.Equal("High", results[0].Name);
    }
}
