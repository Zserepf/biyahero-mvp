using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Generic base DynamoDB repository that provides GetItem, PutItem (with
/// attribute_not_exists conditional), Query, and Delete operations.
///
/// Subclasses define their key schema by implementing the abstract members:
///   - TableName: the DynamoDB table name
///   - PartitionKeyName / SortKeyName: attribute names for pk/sk
///   - ToAttributeMap / FromAttributeMap: AOT-safe serialization (no reflection)
///   - GetPartitionKey / GetSortKey: extract encoded key values from an entity
///
/// This keeps the AWS SDK boundary in the repository layer per the OOP steering,
/// and supports TTL attributes via the ToAttributeMap override.
/// </summary>
/// <typeparam name="T">The domain entity type.</typeparam>
public abstract class BaseDynamoRepository<T> : IDynamoRepository<T> where T : class
{
    private readonly IAmazonDynamoDB _client;

    protected BaseDynamoRepository(IAmazonDynamoDB client)
    {
        _client = client;
    }

    // ─── Abstract Members (subclass defines key schema) ───────────────────

    /// <summary>
    /// The DynamoDB table name.
    /// </summary>
    protected abstract string TableName { get; }

    /// <summary>
    /// The attribute name used as the partition key (e.g., "pk").
    /// </summary>
    protected abstract string PartitionKeyName { get; }

    /// <summary>
    /// The attribute name used as the sort key (e.g., "sk").
    /// </summary>
    protected abstract string SortKeyName { get; }

    /// <summary>
    /// Convert a domain entity to a DynamoDB attribute map.
    /// Must be implemented without reflection for AOT safety.
    /// Should include TTL attributes (e.g., expiresAt as epoch seconds) when applicable.
    /// </summary>
    protected abstract Dictionary<string, AttributeValue> ToAttributeMap(T entity);

    /// <summary>
    /// Reconstruct a domain entity from a DynamoDB attribute map.
    /// Must be implemented without reflection for AOT safety.
    /// </summary>
    protected abstract T FromAttributeMap(Dictionary<string, AttributeValue> attributes);

    /// <summary>
    /// Extract the encoded partition key value from an entity (e.g., "EVENT#{eventId}").
    /// </summary>
    protected abstract string GetPartitionKey(T entity);

    /// <summary>
    /// Extract the encoded sort key value from an entity (e.g., "PING#{commuterId}#{pingId}").
    /// </summary>
    protected abstract string GetSortKey(T entity);

    // ─── IDynamoRepository<T> Implementation ──────────────────────────────

    /// <inheritdoc />
    public async Task<T?> GetItemAsync(string pk, string sk, CancellationToken cancellationToken = default)
    {
        var request = new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [PartitionKeyName] = new AttributeValue { S = pk },
                [SortKeyName] = new AttributeValue { S = sk }
            }
        };

        var response = await _client.GetItemAsync(request, cancellationToken);

        if (response.Item == null || response.Item.Count == 0)
            return null;

        return FromAttributeMap(response.Item);
    }

    /// <inheritdoc />
    public async Task<bool> PutItemAsync(T entity, bool conditionalOnNotExists = false, CancellationToken cancellationToken = default)
    {
        var item = ToAttributeMap(entity);

        var request = new PutItemRequest
        {
            TableName = TableName,
            Item = item
        };

        if (conditionalOnNotExists)
        {
            // Conditional write: only succeeds if the partition key does not already exist.
            // This is the canonical idempotent-webhook pattern (Req 3.7).
            request.ConditionExpression = $"attribute_not_exists({PartitionKeyName})";
        }

        try
        {
            await _client.PutItemAsync(request, cancellationToken);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            // Item already exists — conditional write failed (idempotent no-op).
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> QueryAsync(
        string pk,
        string? skPrefix = null,
        bool scanForward = true,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var request = BuildQueryRequest(
            tableName: TableName,
            indexName: null,
            pkAttributeName: PartitionKeyName,
            pkValue: pk,
            skAttributeName: SortKeyName,
            skPrefix: skPrefix,
            scanForward: scanForward,
            limit: limit);

        return await ExecuteQueryAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> QueryByIndexAsync(
        string indexName,
        string indexPk,
        string? indexSkPrefix = null,
        bool scanForward = true,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        // GSI key attribute names default to the same as the table keys.
        // Subclasses can override GetIndexKeyNames to provide different attribute names.
        var (gsiPkName, gsiSkName) = GetIndexKeyNames(indexName);

        var request = BuildQueryRequest(
            tableName: TableName,
            indexName: indexName,
            pkAttributeName: gsiPkName,
            pkValue: indexPk,
            skAttributeName: gsiSkName,
            skPrefix: indexSkPrefix,
            scanForward: scanForward,
            limit: limit);

        return await ExecuteQueryAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string pk, string sk, CancellationToken cancellationToken = default)
    {
        var request = new DeleteItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [PartitionKeyName] = new AttributeValue { S = pk },
                [SortKeyName] = new AttributeValue { S = sk }
            }
        };

        await _client.DeleteItemAsync(request, cancellationToken);
    }

    // ─── Protected Helpers ────────────────────────────────────────────────

    /// <summary>
    /// Returns the (partitionKeyName, sortKeyName) for a given GSI.
    /// Override in subclasses when GSI key attribute names differ from the table keys.
    /// </summary>
    protected virtual (string PkName, string SkName) GetIndexKeyNames(string indexName)
    {
        return (PartitionKeyName, SortKeyName);
    }

    /// <summary>
    /// Helper to convert an epoch-seconds long to a DynamoDB Number attribute.
    /// Used for TTL attributes.
    /// </summary>
    protected static AttributeValue ToEpochAttribute(DateTime utcDateTime)
    {
        var epoch = new DateTimeOffset(utcDateTime).ToUnixTimeSeconds();
        return new AttributeValue { N = epoch.ToString() };
    }

    /// <summary>
    /// Helper to parse a DynamoDB Number attribute back to a DateTime (from epoch seconds).
    /// </summary>
    protected static DateTime FromEpochAttribute(AttributeValue attribute)
    {
        var epoch = long.Parse(attribute.N);
        return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
    }

    /// <summary>
    /// Helper to safely get a string attribute value, returning null if the attribute is missing.
    /// </summary>
    protected static string? GetStringOrNull(Dictionary<string, AttributeValue> attributes, string key)
    {
        return attributes.TryGetValue(key, out var attr) && attr.S != null ? attr.S : null;
    }

    /// <summary>
    /// Helper to safely get a numeric attribute value as a decimal, returning null if missing.
    /// </summary>
    protected static decimal? GetDecimalOrNull(Dictionary<string, AttributeValue> attributes, string key)
    {
        return attributes.TryGetValue(key, out var attr) && attr.N != null
            ? decimal.Parse(attr.N)
            : null;
    }

    /// <summary>
    /// Helper to safely get a numeric attribute value as a double, returning null if missing.
    /// </summary>
    protected static double? GetDoubleOrNull(Dictionary<string, AttributeValue> attributes, string key)
    {
        return attributes.TryGetValue(key, out var attr) && attr.N != null
            ? double.Parse(attr.N)
            : null;
    }

    // ─── Private Helpers ──────────────────────────────────────────────────

    private static QueryRequest BuildQueryRequest(
        string tableName,
        string? indexName,
        string pkAttributeName,
        string pkValue,
        string skAttributeName,
        string? skPrefix,
        bool scanForward,
        int? limit)
    {
        var expressionValues = new Dictionary<string, AttributeValue>
        {
            [":pk"] = new AttributeValue { S = pkValue }
        };

        var keyCondition = $"{pkAttributeName} = :pk";

        if (!string.IsNullOrEmpty(skPrefix))
        {
            keyCondition += $" AND begins_with({skAttributeName}, :skPrefix)";
            expressionValues[":skPrefix"] = new AttributeValue { S = skPrefix };
        }

        var request = new QueryRequest
        {
            TableName = tableName,
            KeyConditionExpression = keyCondition,
            ExpressionAttributeValues = expressionValues,
            ScanIndexForward = scanForward
        };

        if (!string.IsNullOrEmpty(indexName))
        {
            request.IndexName = indexName;
        }

        if (limit.HasValue)
        {
            request.Limit = limit.Value;
        }

        return request;
    }

    private async Task<IReadOnlyList<T>> ExecuteQueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        var results = new List<T>();

        QueryResponse? response = null;
        do
        {
            if (response?.LastEvaluatedKey?.Count > 0)
            {
                request.ExclusiveStartKey = response.LastEvaluatedKey;
            }

            response = await _client.QueryAsync(request, cancellationToken);

            foreach (var item in response.Items)
            {
                results.Add(FromAttributeMap(item));
            }

            // If a limit was specified and we've reached it, stop paginating.
            if (request.Limit > 0 && results.Count >= request.Limit)
                break;

        } while (response.LastEvaluatedKey?.Count > 0);

        return results;
    }
}
