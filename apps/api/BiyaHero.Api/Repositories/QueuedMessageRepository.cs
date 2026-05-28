using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// DynamoDB repository for queued payment notifications for offline drivers.
///
/// Key schema:
///   PK: USER#{driverId}
///   SK: MSG#{occurredAt}#{eventId}
///
/// The sort key encodes occurredAt (ISO 8601) followed by eventId, ensuring
/// chronological ordering via DynamoDB's lexicographic sort. This supports
/// efficient Query-based drain on driver reconnect.
///
/// TTL: expiresAt — 24 hours after enqueue time (Req 3.6 retention window).
///
/// Requirement: 3.6
/// </summary>
public sealed class QueuedMessageRepository : BaseDynamoRepository<QueuedMessage>, IQueuedMessageRepository
{
    private const string QueuedMessagesTable = "QueuedMessages";
    private const int TtlHours = 24;

    private readonly IAmazonDynamoDB _dynamoClient;

    public QueuedMessageRepository(IAmazonDynamoDB client) : base(client)
    {
        _dynamoClient = client;
    }

    // ─── Abstract Member Implementations ──────────────────────────────────

    protected override string TableName => QueuedMessagesTable;
    protected override string PartitionKeyName => "pk";
    protected override string SortKeyName => "sk";

    protected override string GetPartitionKey(QueuedMessage entity)
        => $"USER#{entity.DriverId}";

    protected override string GetSortKey(QueuedMessage entity)
        => $"MSG#{entity.OccurredAt:o}#{entity.EventId}";

    protected override Dictionary<string, AttributeValue> ToAttributeMap(QueuedMessage entity)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = GetPartitionKey(entity) },
            ["sk"] = new AttributeValue { S = GetSortKey(entity) },
            ["id"] = new AttributeValue { S = entity.Id.ToString() },
            ["driverId"] = new AttributeValue { S = entity.DriverId.ToString() },
            ["eventId"] = new AttributeValue { S = entity.EventId },
            ["occurredAt"] = new AttributeValue { S = entity.OccurredAt.ToString("o") },
            ["payload"] = new AttributeValue { S = entity.Payload },
            ["createdAt"] = new AttributeValue { S = entity.CreatedAt.ToString("o") },
            ["updatedAt"] = new AttributeValue { S = entity.UpdatedAt.ToString("o") },
            ["expiresAt"] = ToEpochAttribute(entity.ExpiresAt)
        };
    }

    protected override QueuedMessage FromAttributeMap(Dictionary<string, AttributeValue> attributes)
    {
        return new QueuedMessage(
            id: Guid.Parse(attributes["id"].S),
            createdAt: DateTime.Parse(attributes["createdAt"].S, null, System.Globalization.DateTimeStyles.RoundtripKind),
            updatedAt: DateTime.Parse(attributes["updatedAt"].S, null, System.Globalization.DateTimeStyles.RoundtripKind),
            driverId: Guid.Parse(attributes["driverId"].S),
            eventId: attributes["eventId"].S,
            occurredAt: DateTime.Parse(attributes["occurredAt"].S, null, System.Globalization.DateTimeStyles.RoundtripKind),
            payload: attributes["payload"].S,
            expiresAt: FromEpochAttribute(attributes["expiresAt"])
        );
    }

    // ─── IQueuedMessageRepository Implementation ─────────────────────────

    /// <inheritdoc />
    public async Task EnqueueAsync(Guid driverId, string eventId, DateTime occurredAt, string payload, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var message = new QueuedMessage(
            id: Guid.NewGuid(),
            createdAt: now,
            updatedAt: now,
            driverId: driverId,
            eventId: eventId,
            occurredAt: occurredAt,
            payload: payload,
            expiresAt: now.AddHours(TtlHours)
        );

        await PutItemAsync(message, conditionalOnNotExists: false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueuedMessage>> DrainAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var pk = $"USER#{driverId}";

        // Query all messages for this driver, sorted chronologically (scanForward: true).
        var messages = await QueryAsync(pk, skPrefix: "MSG#", scanForward: true, cancellationToken: cancellationToken);

        if (messages.Count == 0)
            return messages;

        // Batch-delete all drained messages.
        // DynamoDB BatchWriteItem supports up to 25 items per request.
        var deleteRequests = messages.Select(msg => new WriteRequest
        {
            DeleteRequest = new DeleteRequest
            {
                Key = new Dictionary<string, AttributeValue>
                {
                    [PartitionKeyName] = new AttributeValue { S = GetPartitionKey(msg) },
                    [SortKeyName] = new AttributeValue { S = GetSortKey(msg) }
                }
            }
        }).ToList();

        // Process in batches of 25 (DynamoDB limit).
        const int batchSize = 25;
        for (var i = 0; i < deleteRequests.Count; i += batchSize)
        {
            var batch = deleteRequests.Skip(i).Take(batchSize).ToList();
            var batchRequest = new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [TableName] = batch
                }
            };

            BatchWriteItemResponse response;
            do
            {
                response = await _dynamoClient.BatchWriteItemAsync(batchRequest, cancellationToken);

                // Retry any unprocessed items (throttling / capacity).
                if (response.UnprocessedItems?.Count > 0)
                {
                    batchRequest.RequestItems = response.UnprocessedItems;
                    await Task.Delay(100, cancellationToken); // Brief backoff before retry.
                }
            } while (response.UnprocessedItems?.Count > 0);
        }

        return messages;
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var pk = $"USER#{driverId}";

        // Use Select.COUNT to avoid transferring item data — just get the count.
        var request = new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = $"{PartitionKeyName} = :pk AND begins_with({SortKeyName}, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = pk },
                [":skPrefix"] = new AttributeValue { S = "MSG#" }
            },
            Select = Select.COUNT
        };

        var count = 0;
        QueryResponse? response = null;
        do
        {
            if (response?.LastEvaluatedKey?.Count > 0)
            {
                request.ExclusiveStartKey = response.LastEvaluatedKey;
            }

            response = await _dynamoClient.QueryAsync(request, cancellationToken);
            count += response.Count;
        } while (response.LastEvaluatedKey?.Count > 0);

        return count;
    }
}
