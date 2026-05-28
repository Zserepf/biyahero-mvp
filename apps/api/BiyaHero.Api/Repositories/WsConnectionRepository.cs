using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// DynamoDB repository for WebSocket connection tracking.
/// Table key schema: PK = USER#{userId}, SK = CONN#{connectionId}.
/// GSI "byConnectionId": PK = connectionId (for fast $disconnect cleanup).
/// 24-hour TTL safety net via expiresAt attribute.
/// Requirements: 3.2, 3.6, 4.3, 5.4
/// </summary>
public class WsConnectionRepository : BaseDynamoRepository<WsConnection>, IWsConnectionRepository
{
    private const string GsiByConnectionId = "byConnectionId";
    private const string GsiPkAttribute = "connectionId";

    private readonly IAmazonDynamoDB _dynamoClient;

    public WsConnectionRepository(IAmazonDynamoDB client) : base(client)
    {
        _dynamoClient = client;
    }

    // ─── Abstract Member Implementations ──────────────────────────────────

    protected override string TableName => "WsConnections";

    protected override string PartitionKeyName => "pk";

    protected override string SortKeyName => "sk";

    protected override string GetPartitionKey(WsConnection entity)
        => $"USER#{entity.UserId}";

    protected override string GetSortKey(WsConnection entity)
        => $"CONN#{entity.ConnectionId}";

    protected override (string PkName, string SkName) GetIndexKeyNames(string indexName)
    {
        if (indexName == GsiByConnectionId)
            return (GsiPkAttribute, SortKeyName);

        return base.GetIndexKeyNames(indexName);
    }

    protected override Dictionary<string, AttributeValue> ToAttributeMap(WsConnection entity)
    {
        var map = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = GetPartitionKey(entity) },
            ["sk"] = new AttributeValue { S = GetSortKey(entity) },
            ["id"] = new AttributeValue { S = entity.Id.ToString() },
            ["userId"] = new AttributeValue { S = entity.UserId.ToString() },
            ["connectionId"] = new AttributeValue { S = entity.ConnectionId },
            ["role"] = new AttributeValue { S = entity.Role.ToString() },
            ["connectedAt"] = new AttributeValue { S = entity.ConnectedAt.ToString("o") },
            ["createdAt"] = new AttributeValue { S = entity.CreatedAt.ToString("o") },
            ["updatedAt"] = new AttributeValue { S = entity.UpdatedAt.ToString("o") },
            ["expiresAt"] = ToEpochAttribute(entity.ExpiresAt)
        };

        if (!string.IsNullOrEmpty(entity.SubscribedBbox))
        {
            map["subscribedBbox"] = new AttributeValue { S = entity.SubscribedBbox };
        }

        return map;
    }

    protected override WsConnection FromAttributeMap(Dictionary<string, AttributeValue> attributes)
    {
        return new WsConnection(
            id: Guid.Parse(attributes["id"].S),
            createdAt: DateTime.Parse(attributes["createdAt"].S),
            updatedAt: DateTime.Parse(attributes["updatedAt"].S),
            userId: Guid.Parse(attributes["userId"].S),
            role: Enum.Parse<UserRole>(attributes["role"].S),
            connectionId: attributes["connectionId"].S,
            connectedAt: DateTime.Parse(attributes["connectedAt"].S),
            subscribedBbox: GetStringOrNull(attributes, "subscribedBbox"),
            expiresAt: FromEpochAttribute(attributes["expiresAt"])
        );
    }

    // ─── IWsConnectionRepository Implementation ──────────────────────────

    /// <inheritdoc />
    public async Task RegisterConnectionAsync(WsConnection connection, CancellationToken cancellationToken = default)
    {
        await PutItemAsync(connection, conditionalOnNotExists: false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        // Query the GSI to find the connection record (which gives us the PK/SK).
        var connections = await QueryByIndexAsync(
            indexName: GsiByConnectionId,
            indexPk: connectionId,
            cancellationToken: cancellationToken);

        if (connections.Count == 0)
            return; // Connection already removed or never existed — idempotent no-op.

        var conn = connections[0];
        await DeleteAsync(GetPartitionKey(conn), GetSortKey(conn), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WsConnection>> GetConnectionsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var pk = $"USER#{userId}";
        return await QueryAsync(pk, skPrefix: "CONN#", cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WsConnection?> GetConnectionByIdAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        var connections = await QueryByIndexAsync(
            indexName: GsiByConnectionId,
            indexPk: connectionId,
            limit: 1,
            cancellationToken: cancellationToken);

        return connections.Count > 0 ? connections[0] : null;
    }

    /// <inheritdoc />
    public async Task UpdateSubscriptionAsync(string connectionId, string? bbox, CancellationToken cancellationToken = default)
    {
        // Find the connection to get its PK/SK.
        var connection = await GetConnectionByIdAsync(connectionId, cancellationToken);
        if (connection == null)
            return; // Connection not found — no-op.

        // Update the subscribedBbox field and re-persist.
        connection.SubscribedBbox = bbox;
        connection.UpdatedAt = DateTime.UtcNow;
        await PutItemAsync(connection, conditionalOnNotExists: false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WsConnection>> GetSubscribedConnectionsAsync(CancellationToken cancellationToken = default)
    {
        // Scan the table for connections with a non-null subscribedBbox.
        // At MVP scale (≤1000 MAU) this is acceptable; at scale, consider a sparse GSI
        // keyed on subscribedBbox for efficient fan-out queries.
        var results = new List<WsConnection>();

        var request = new ScanRequest
        {
            TableName = TableName,
            FilterExpression = "attribute_exists(subscribedBbox)"
        };

        ScanResponse? response = null;
        do
        {
            if (response?.LastEvaluatedKey?.Count > 0)
            {
                request.ExclusiveStartKey = response.LastEvaluatedKey;
            }

            response = await _dynamoClient.ScanAsync(request, cancellationToken);

            foreach (var item in response.Items)
            {
                results.Add(FromAttributeMap(item));
            }
        } while (response.LastEvaluatedKey?.Count > 0);

        return results;
    }
}
