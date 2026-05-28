using System.Data;
using System.Linq.Expressions;
using BiyaHero.Api.Domain;
using Dapper;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Base PostgreSQL repository using Dapper for AOT-safe data access.
/// Provides generic CRUD and transactional helpers.
/// SQL strings are encapsulated here — handlers and domain classes never see raw SQL.
/// 
/// Subclasses must provide:
///   - TableName: the PostgreSQL table name
///   - MapToEntity: convert a dynamic row to a typed entity
///   - GetInsertSql/GetInsertParameters: SQL + params for creating a new row
///   - GetUpdateSql/GetUpdateParameters: SQL + params for updating an existing row
/// </summary>
public abstract class BasePostgresRepository<T> : IRepository<T> where T : BaseDomain
{
    private readonly IDbConnectionFactory _connectionFactory;

    protected BasePostgresRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    // ─── Abstract Members (subclasses define table-specific behavior) ─────

    /// <summary>
    /// The PostgreSQL table name for this entity.
    /// </summary>
    protected abstract string TableName { get; }

    /// <summary>
    /// Maps a database row (dynamic) to a strongly-typed domain entity.
    /// </summary>
    protected abstract T MapToEntity(dynamic row);

    /// <summary>
    /// Returns the INSERT SQL statement for this entity.
    /// Use parameterized placeholders (e.g., @Id, @Name).
    /// </summary>
    protected abstract string GetInsertSql();

    /// <summary>
    /// Returns the parameter object for the INSERT statement.
    /// </summary>
    protected abstract object GetInsertParameters(T entity);

    /// <summary>
    /// Returns the UPDATE SQL statement for this entity.
    /// Use parameterized placeholders (e.g., @Id, @Name).
    /// </summary>
    protected abstract string GetUpdateSql();

    /// <summary>
    /// Returns the parameter object for the UPDATE statement.
    /// </summary>
    protected abstract object GetUpdateParameters(T entity);

    // ─── IRepository<T> Implementation ────────────────────────────────────

    public virtual async Task<T?> FindByIdAsync(Guid id)
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;
        var sql = $"SELECT * FROM {TableName} WHERE id = @Id LIMIT 1";
        var row = await dbConnection.QueryFirstOrDefaultAsync(sql, new { Id = id });

        return row is null ? null : MapToEntity(row);
    }

    public virtual async Task<IReadOnlyList<T>> FindAllAsync()
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;
        var sql = $"SELECT * FROM {TableName} ORDER BY created_at DESC";
        var rows = await dbConnection.QueryAsync(sql);

        return rows.Select(row => MapToEntity((object)row)).ToList().AsReadOnly();
    }

    public virtual async Task<T> CreateAsync(T entity)
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;
        var sql = GetInsertSql();
        var parameters = GetInsertParameters(entity);

        await dbConnection.ExecuteAsync(sql, parameters);
        return entity;
    }

    public virtual async Task<IReadOnlyList<T>> WhereAsync(Expression<Func<T, bool>> predicate)
    {
        // For the base implementation, WhereAsync loads all and filters in-memory.
        // Subclasses should override with optimized SQL queries for their specific use cases.
        var all = await FindAllAsync();
        var compiled = predicate.Compile();
        return all.Where(compiled).ToList().AsReadOnly();
    }

    public virtual async Task<T> SaveAsync(T entity)
    {
        // Save = upsert: try update, if no rows affected, insert
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;

        var existsSql = $"SELECT COUNT(1) FROM {TableName} WHERE id = @Id";
        var exists = await dbConnection.ExecuteScalarAsync<int>(existsSql, new { entity.Id }) > 0;

        if (exists)
        {
            var updateSql = GetUpdateSql();
            var updateParams = GetUpdateParameters(entity);
            await dbConnection.ExecuteAsync(updateSql, updateParams);
        }
        else
        {
            var insertSql = GetInsertSql();
            var insertParams = GetInsertParameters(entity);
            await dbConnection.ExecuteAsync(insertSql, insertParams);
        }

        return entity;
    }

    public virtual async Task<T> UpdateAsync(T entity)
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;
        var sql = GetUpdateSql();
        var parameters = GetUpdateParameters(entity);

        var affected = await dbConnection.ExecuteAsync(sql, parameters);
        if (affected == 0)
        {
            throw new InvalidOperationException($"Entity with id {entity.Id} not found in {TableName}");
        }

        return entity;
    }

    public virtual async Task DeleteAsync(T entity)
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;
        var sql = $"DELETE FROM {TableName} WHERE id = @Id";
        await dbConnection.ExecuteAsync(sql, new { entity.Id });
    }

    // ─── Transactional Helpers ────────────────────────────────────────────

    /// <summary>
    /// Executes multiple operations within a single database transaction.
    /// If any operation fails, the entire transaction is rolled back.
    /// </summary>
    protected async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IDbConnection, IDbTransaction, Task<TResult>> operation)
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;
        using var transaction = dbConnection.BeginTransaction();

        try
        {
            var result = await operation(dbConnection, transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Executes multiple operations within a single database transaction (no return value).
    /// If any operation fails, the entire transaction is rolled back.
    /// </summary>
    protected async Task ExecuteInTransactionAsync(
        Func<IDbConnection, IDbTransaction, Task> operation)
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;
        using var transaction = dbConnection.BeginTransaction();

        try
        {
            await operation(dbConnection, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Executes a raw parameterized query within a transaction.
    /// Used by subclasses for complex multi-table operations (e.g., Route + Waypoints).
    /// SQL is still encapsulated within the repository layer.
    /// </summary>
    protected async Task<IEnumerable<dynamic>> QueryInTransactionAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string sql,
        object? parameters = null)
    {
        return await connection.QueryAsync(sql, parameters, transaction);
    }

    /// <summary>
    /// Executes a raw parameterized command within a transaction.
    /// Used by subclasses for complex multi-table operations.
    /// SQL is still encapsulated within the repository layer.
    /// </summary>
    protected async Task<int> ExecuteInTransactionAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string sql,
        object? parameters = null)
    {
        return await connection.ExecuteAsync(sql, parameters, transaction);
    }

    /// <summary>
    /// Executes a parameterized query and returns mapped entities.
    /// Used by subclasses for custom queries (e.g., bbox spatial queries).
    /// SQL is still encapsulated within the repository layer.
    /// </summary>
    protected async Task<IReadOnlyList<T>> QueryAsync(string sql, object? parameters = null)
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;
        var rows = await dbConnection.QueryAsync(sql, parameters);

        return rows.Select(row => MapToEntity((object)row)).ToList().AsReadOnly();
    }

    /// <summary>
    /// Executes a parameterized scalar query.
    /// Used by subclasses for count or existence checks.
    /// </summary>
    protected async Task<TScalar?> ScalarAsync<TScalar>(string sql, object? parameters = null)
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;
        return await dbConnection.ExecuteScalarAsync<TScalar>(sql, parameters);
    }
}
