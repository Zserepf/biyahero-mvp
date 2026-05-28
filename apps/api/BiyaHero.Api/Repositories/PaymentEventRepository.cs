using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// DynamoDB repository for PaymentEvent entities.
///
/// Key schema:
///   PK: EVENT#{eventId}
///   SK: EVENT#{eventId} (single-item pattern for simple GetItem)
///
/// GSI byDriverId:
///   PK: driverId (string GUID)
///   SK: occurredAt (ISO 8601 string for lexicographic sort)
///
/// TTL: expiresAt — 90 days after occurredAt (Req 8.2)
///
/// Conditional write (attribute_not_exists on pk) enforces idempotent
/// webhook processing per the Anti-123 pattern (Req 3.7).
///
/// Validates: Requirements 3.1, 3.7, 8.2
/// </summary>
public sealed class PaymentEventRepository : BaseDynamoRepository<PaymentEvent>, IPaymentEventRepository
{
    private const string PaymentEventsTable = "PaymentEvents";
    private const string ByDriverIdIndex = "byDriverId";
    private const int TtlDays = 90;

    public PaymentEventRepository(IAmazonDynamoDB client) : base(client) { }

    // ─── Abstract Member Implementations ──────────────────────────────────

    protected override string TableName => PaymentEventsTable;
    protected override string PartitionKeyName => "pk";
    protected override string SortKeyName => "sk";

    protected override string GetPartitionKey(PaymentEvent entity)
        => $"EVENT#{entity.EventId}";

    protected override string GetSortKey(PaymentEvent entity)
        => $"EVENT#{entity.EventId}";

    protected override Dictionary<string, AttributeValue> ToAttributeMap(PaymentEvent entity)
    {
        var map = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = GetPartitionKey(entity) },
            ["sk"] = new AttributeValue { S = GetSortKey(entity) },
            ["eventId"] = new AttributeValue { S = entity.EventId },
            ["idempotencyKey"] = new AttributeValue { S = entity.IdempotencyKey },
            ["id"] = new AttributeValue { S = entity.Id.ToString() },
            ["driverId"] = new AttributeValue { S = entity.DriverId.ToString() },
            ["payerId"] = new AttributeValue { S = entity.PayerId.ToString() },
            ["payerName"] = new AttributeValue { S = entity.PayerName },
            ["routeId"] = new AttributeValue { S = entity.RouteId.ToString() },
            ["amountCentavos"] = new AttributeValue { N = entity.AmountCentavos.ToString() },
            ["currency"] = new AttributeValue { S = entity.Currency },
            ["status"] = new AttributeValue { S = entity.Status.ToString() },
            ["walletProvider"] = new AttributeValue { S = entity.WalletProvider },
            ["occurredAt"] = new AttributeValue { S = entity.OccurredAt.ToString("o") },
            ["webhookTimestamp"] = new AttributeValue { S = entity.WebhookTimestamp.ToString("o") },
            ["rawPayloadHash"] = new AttributeValue { S = entity.RawPayloadHash },
            ["createdAt"] = new AttributeValue { S = entity.CreatedAt.ToString("o") },
            ["updatedAt"] = new AttributeValue { S = entity.UpdatedAt.ToString("o") },
            ["expiresAt"] = ToEpochAttribute(entity.OccurredAt.AddDays(TtlDays))
        };

        // Optional attributes — only write if non-null to avoid empty-string DynamoDB errors
        if (!string.IsNullOrEmpty(entity.WalletTransactionId))
        {
            map["walletTransactionId"] = new AttributeValue { S = entity.WalletTransactionId };
        }

        if (entity.ProcessedTimestamp.HasValue)
        {
            map["processedTimestamp"] = new AttributeValue { S = entity.ProcessedTimestamp.Value.ToString("o") };
        }

        return map;
    }

    protected override PaymentEvent FromAttributeMap(Dictionary<string, AttributeValue> attributes)
    {
        var id = Guid.Parse(attributes["id"].S);
        var createdAt = DateTime.Parse(attributes["createdAt"].S, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var updatedAt = DateTime.Parse(attributes["updatedAt"].S, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var eventId = attributes["eventId"].S;
        var idempotencyKey = attributes["idempotencyKey"].S;
        var driverId = Guid.Parse(attributes["driverId"].S);
        var payerId = Guid.Parse(attributes["payerId"].S);
        var payerName = attributes["payerName"].S;
        var routeId = Guid.Parse(attributes["routeId"].S);
        var amountCentavos = int.Parse(attributes["amountCentavos"].N);
        var currency = attributes["currency"].S;
        var status = Enum.Parse<PaymentStatus>(attributes["status"].S);
        var walletProvider = attributes["walletProvider"].S;
        var walletTransactionId = GetStringOrNull(attributes, "walletTransactionId");
        var occurredAt = DateTime.Parse(attributes["occurredAt"].S, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var webhookTimestamp = DateTime.Parse(attributes["webhookTimestamp"].S, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var rawPayloadHash = attributes["rawPayloadHash"].S;

        DateTime? processedTimestamp = null;
        var processedStr = GetStringOrNull(attributes, "processedTimestamp");
        if (processedStr != null)
        {
            processedTimestamp = DateTime.Parse(processedStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        return new PaymentEvent(
            id, createdAt, updatedAt,
            eventId, idempotencyKey,
            driverId, payerId, payerName, routeId,
            amountCentavos, currency, status,
            walletProvider, walletTransactionId,
            occurredAt, webhookTimestamp, processedTimestamp,
            rawPayloadHash);
    }

    protected override (string PkName, string SkName) GetIndexKeyNames(string indexName)
    {
        if (indexName == ByDriverIdIndex)
            return ("driverId", "occurredAt");

        return base.GetIndexKeyNames(indexName);
    }

    // ─── IPaymentEventRepository Implementation ──────────────────────────

    /// <inheritdoc />
    public async Task<bool> PutEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken = default)
    {
        // Conditional write: attribute_not_exists(pk) enforces idempotence (Req 3.7).
        // Returns true if new event was written, false if duplicate (already exists).
        return await PutItemAsync(paymentEvent, conditionalOnNotExists: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PaymentEvent?> GetEventByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var pk = $"EVENT#{eventId}";
        var sk = $"EVENT#{eventId}";
        return await GetItemAsync(pk, sk, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentEvent>> GetEventsByDriverAsync(Guid driverId, int limit, CancellationToken cancellationToken = default)
    {
        // Query GSI byDriverId with driverId as PK, sorted by occurredAt descending (most recent first).
        return await QueryByIndexAsync(
            indexName: ByDriverIdIndex,
            indexPk: driverId.ToString(),
            indexSkPrefix: null,
            scanForward: false,
            limit: limit,
            cancellationToken: cancellationToken);
    }
}
