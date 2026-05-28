using Amazon.DynamoDBv2.Model;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Generic DynamoDB repository interface for entities stored in DynamoDB.
/// Abstracts over partition key + sort key encoding so entity-specific
/// repositories just define their key schema.
/// </summary>
/// <typeparam name="T">The domain entity type.</typeparam>
public interface IDynamoRepository<T> where T : class
{
    /// <summary>
    /// Retrieve a single item by its partition key and sort key.
    /// Returns null if the item does not exist.
    /// </summary>
    Task<T?> GetItemAsync(string pk, string sk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Put an item into the table. If <paramref name="conditionalOnNotExists"/> is true,
    /// the write uses attribute_not_exists(pk) to enforce idempotence — the write
    /// fails silently (returns false) if the item already exists.
    /// </summary>
    /// <returns>True if the item was written; false if the conditional check failed (item already exists).</returns>
    Task<bool> PutItemAsync(T entity, bool conditionalOnNotExists = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query items by partition key with an optional sort key prefix condition.
    /// Returns items in sort key order (ascending by default).
    /// </summary>
    Task<IReadOnlyList<T>> QueryAsync(
        string pk,
        string? skPrefix = null,
        bool scanForward = true,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query items using a Global Secondary Index by the GSI partition key
    /// with an optional GSI sort key prefix condition.
    /// </summary>
    Task<IReadOnlyList<T>> QueryByIndexAsync(
        string indexName,
        string indexPk,
        string? indexSkPrefix = null,
        bool scanForward = true,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a single item by its partition key and sort key.
    /// </summary>
    Task DeleteAsync(string pk, string sk, CancellationToken cancellationToken = default);
}
