using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Tests.Repositories;

/// <summary>
/// A simple test entity to exercise BaseDynamoRepository without coupling to real domain classes.
/// </summary>
public sealed class TestDynamoEntity
{
    public string EventId { get; set; } = string.Empty;
    public string DriverId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Concrete implementation of BaseDynamoRepository for testing purposes.
/// Demonstrates the key-encoding abstraction pattern.
/// </summary>
public sealed class TestDynamoRepository : BaseDynamoRepository<TestDynamoEntity>
{
    public TestDynamoRepository(IAmazonDynamoDB client) : base(client) { }

    protected override string TableName => "TestEvents";
    protected override string PartitionKeyName => "pk";
    protected override string SortKeyName => "sk";

    protected override string GetPartitionKey(TestDynamoEntity entity)
        => $"EVENT#{entity.EventId}";

    protected override string GetSortKey(TestDynamoEntity entity)
        => $"EVENT#{entity.EventId}";

    protected override Dictionary<string, AttributeValue> ToAttributeMap(TestDynamoEntity entity)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = GetPartitionKey(entity) },
            ["sk"] = new AttributeValue { S = GetSortKey(entity) },
            ["eventId"] = new AttributeValue { S = entity.EventId },
            ["driverId"] = new AttributeValue { S = entity.DriverId },
            ["amountCentavos"] = new AttributeValue { N = ((int)(entity.Amount * 100)).ToString() },
            ["expiresAt"] = ToEpochAttribute(entity.ExpiresAt)
        };
    }

    protected override TestDynamoEntity FromAttributeMap(Dictionary<string, AttributeValue> attributes)
    {
        return new TestDynamoEntity
        {
            EventId = attributes["eventId"].S,
            DriverId = attributes["driverId"].S,
            Amount = decimal.Parse(attributes["amountCentavos"].N) / 100m,
            ExpiresAt = FromEpochAttribute(attributes["expiresAt"])
        };
    }

    protected override (string PkName, string SkName) GetIndexKeyNames(string indexName)
    {
        if (indexName == "byDriverId")
            return ("driverId", "occurredAt");
        return base.GetIndexKeyNames(indexName);
    }
}

/// <summary>
/// Fake IAmazonDynamoDB that records calls and returns canned responses.
/// Allows testing BaseDynamoRepository logic without a real DynamoDB connection.
/// </summary>
public sealed class FakeDynamoDbClient : IAmazonDynamoDB
{
    public List<PutItemRequest> PutItemCalls { get; } = new();
    public List<GetItemRequest> GetItemCalls { get; } = new();
    public List<QueryRequest> QueryCalls { get; } = new();
    public List<DeleteItemRequest> DeleteItemCalls { get; } = new();

    /// <summary>
    /// When set to true, PutItem will throw ConditionalCheckFailedException.
    /// </summary>
    public bool SimulateConditionalFailure { get; set; }

    /// <summary>
    /// Items to return from GetItem calls.
    /// </summary>
    public Dictionary<string, AttributeValue>? GetItemResponse { get; set; }

    /// <summary>
    /// Items to return from Query calls.
    /// </summary>
    public List<Dictionary<string, AttributeValue>> QueryResponse { get; set; } = new();

    public Task<PutItemResponse> PutItemAsync(PutItemRequest request, CancellationToken cancellationToken = default)
    {
        PutItemCalls.Add(request);
        if (SimulateConditionalFailure && request.ConditionExpression != null)
            throw new ConditionalCheckFailedException("Conditional check failed");
        return Task.FromResult(new PutItemResponse());
    }

    public Task<GetItemResponse> GetItemAsync(GetItemRequest request, CancellationToken cancellationToken = default)
    {
        GetItemCalls.Add(request);
        var response = new GetItemResponse();
        if (GetItemResponse != null)
            response.Item = GetItemResponse;
        return Task.FromResult(response);
    }

    public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        QueryCalls.Add(request);
        var response = new QueryResponse { Items = QueryResponse };
        return Task.FromResult(response);
    }

    public Task<DeleteItemResponse> DeleteItemAsync(DeleteItemRequest request, CancellationToken cancellationToken = default)
    {
        DeleteItemCalls.Add(request);
        return Task.FromResult(new DeleteItemResponse());
    }

    // ─── Unused IAmazonDynamoDB members (minimal stubs) ───────────────────

    public IClientConfig Config => throw new NotImplementedException();
    public IDynamoDBv2PaginatorFactory Paginators => throw new NotImplementedException();

    public void Dispose() { }

    // All other interface members throw NotImplementedException — only the four
    // methods used by BaseDynamoRepository are implemented above.

    public Task<TransactGetItemsResponse> TransactGetItemsAsync(TransactGetItemsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<TransactWriteItemsResponse> TransactWriteItemsAsync(TransactWriteItemsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Amazon.Runtime.Endpoints.Endpoint DetermineServiceOperationEndpoint(AmazonWebServiceRequest request) => throw new NotImplementedException();

    public Task<BatchExecuteStatementResponse> BatchExecuteStatementAsync(BatchExecuteStatementRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<BatchGetItemResponse> BatchGetItemAsync(BatchGetItemRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<BatchGetItemResponse> BatchGetItemAsync(Dictionary<string, KeysAndAttributes> requestItems, ReturnConsumedCapacity returnConsumedCapacity, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<BatchGetItemResponse> BatchGetItemAsync(Dictionary<string, KeysAndAttributes> requestItems, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<BatchWriteItemResponse> BatchWriteItemAsync(BatchWriteItemRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<BatchWriteItemResponse> BatchWriteItemAsync(Dictionary<string, List<WriteRequest>> requestItems, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<CreateBackupResponse> CreateBackupAsync(CreateBackupRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<CreateGlobalTableResponse> CreateGlobalTableAsync(CreateGlobalTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<CreateTableResponse> CreateTableAsync(CreateTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<CreateTableResponse> CreateTableAsync(string tableName, List<KeySchemaElement> keySchema, List<AttributeDefinition> attributeDefinitions, ProvisionedThroughput provisionedThroughput, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DeleteBackupResponse> DeleteBackupAsync(DeleteBackupRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DeleteItemResponse> DeleteItemAsync(string tableName, Dictionary<string, AttributeValue> key, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DeleteItemResponse> DeleteItemAsync(string tableName, Dictionary<string, AttributeValue> key, ReturnValue returnValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DeleteResourcePolicyResponse> DeleteResourcePolicyAsync(DeleteResourcePolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DeleteTableResponse> DeleteTableAsync(DeleteTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DeleteTableResponse> DeleteTableAsync(string tableName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeBackupResponse> DescribeBackupAsync(DescribeBackupRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeContinuousBackupsResponse> DescribeContinuousBackupsAsync(DescribeContinuousBackupsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeContributorInsightsResponse> DescribeContributorInsightsAsync(DescribeContributorInsightsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeEndpointsResponse> DescribeEndpointsAsync(DescribeEndpointsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeExportResponse> DescribeExportAsync(DescribeExportRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeGlobalTableResponse> DescribeGlobalTableAsync(DescribeGlobalTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeGlobalTableSettingsResponse> DescribeGlobalTableSettingsAsync(DescribeGlobalTableSettingsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeImportResponse> DescribeImportAsync(DescribeImportRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeKinesisStreamingDestinationResponse> DescribeKinesisStreamingDestinationAsync(DescribeKinesisStreamingDestinationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeLimitsResponse> DescribeLimitsAsync(DescribeLimitsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeTableResponse> DescribeTableAsync(DescribeTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeTableResponse> DescribeTableAsync(string tableName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeTableReplicaAutoScalingResponse> DescribeTableReplicaAutoScalingAsync(DescribeTableReplicaAutoScalingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeTimeToLiveResponse> DescribeTimeToLiveAsync(DescribeTimeToLiveRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeTimeToLiveResponse> DescribeTimeToLiveAsync(string tableName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DisableKinesisStreamingDestinationResponse> DisableKinesisStreamingDestinationAsync(DisableKinesisStreamingDestinationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<EnableKinesisStreamingDestinationResponse> EnableKinesisStreamingDestinationAsync(EnableKinesisStreamingDestinationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ExecuteStatementResponse> ExecuteStatementAsync(ExecuteStatementRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ExecuteTransactionResponse> ExecuteTransactionAsync(ExecuteTransactionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ExportTableToPointInTimeResponse> ExportTableToPointInTimeAsync(ExportTableToPointInTimeRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<GetItemResponse> GetItemAsync(string tableName, Dictionary<string, AttributeValue> key, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<GetItemResponse> GetItemAsync(string tableName, Dictionary<string, AttributeValue> key, bool consistentRead, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<GetResourcePolicyResponse> GetResourcePolicyAsync(GetResourcePolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ImportTableResponse> ImportTableAsync(ImportTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListBackupsResponse> ListBackupsAsync(ListBackupsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListContributorInsightsResponse> ListContributorInsightsAsync(ListContributorInsightsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListExportsResponse> ListExportsAsync(ListExportsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListGlobalTablesResponse> ListGlobalTablesAsync(ListGlobalTablesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListImportsResponse> ListImportsAsync(ListImportsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListTablesResponse> ListTablesAsync(ListTablesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListTablesResponse> ListTablesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListTablesResponse> ListTablesAsync(string exclusiveStartTableName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListTablesResponse> ListTablesAsync(string exclusiveStartTableName, int limit, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListTablesResponse> ListTablesAsync(int limit, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListTagsOfResourceResponse> ListTagsOfResourceAsync(ListTagsOfResourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutItemResponse> PutItemAsync(string tableName, Dictionary<string, AttributeValue> item, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutItemResponse> PutItemAsync(string tableName, Dictionary<string, AttributeValue> item, ReturnValue returnValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutResourcePolicyResponse> PutResourcePolicyAsync(PutResourcePolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<RestoreTableFromBackupResponse> RestoreTableFromBackupAsync(RestoreTableFromBackupRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<RestoreTableToPointInTimeResponse> RestoreTableToPointInTimeAsync(RestoreTableToPointInTimeRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ScanResponse> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ScanResponse> ScanAsync(string tableName, List<string> attributesToGet, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ScanResponse> ScanAsync(string tableName, Dictionary<string, Condition> scanFilter, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ScanResponse> ScanAsync(string tableName, List<string> attributesToGet, Dictionary<string, Condition> scanFilter, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<TagResourceResponse> TagResourceAsync(TagResourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UntagResourceResponse> UntagResourceAsync(UntagResourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UpdateContinuousBackupsResponse> UpdateContinuousBackupsAsync(UpdateContinuousBackupsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UpdateContributorInsightsResponse> UpdateContributorInsightsAsync(UpdateContributorInsightsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UpdateGlobalTableResponse> UpdateGlobalTableAsync(UpdateGlobalTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UpdateGlobalTableSettingsResponse> UpdateGlobalTableSettingsAsync(UpdateGlobalTableSettingsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UpdateItemResponse> UpdateItemAsync(UpdateItemRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UpdateItemResponse> UpdateItemAsync(string tableName, Dictionary<string, AttributeValue> key, Dictionary<string, AttributeValueUpdate> attributeUpdates, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UpdateItemResponse> UpdateItemAsync(string tableName, Dictionary<string, AttributeValue> key, Dictionary<string, AttributeValueUpdate> attributeUpdates, ReturnValue returnValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UpdateKinesisStreamingDestinationResponse> UpdateKinesisStreamingDestinationAsync(UpdateKinesisStreamingDestinationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UpdateTableResponse> UpdateTableAsync(UpdateTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UpdateTableResponse> UpdateTableAsync(string tableName, ProvisionedThroughput provisionedThroughput, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UpdateTableReplicaAutoScalingResponse> UpdateTableReplicaAutoScalingAsync(UpdateTableReplicaAutoScalingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UpdateTimeToLiveResponse> UpdateTimeToLiveAsync(UpdateTimeToLiveRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}

public class BaseDynamoRepositoryTests
{
    private readonly FakeDynamoDbClient _fakeClient;
    private readonly TestDynamoRepository _repository;

    public BaseDynamoRepositoryTests()
    {
        _fakeClient = new FakeDynamoDbClient();
        _repository = new TestDynamoRepository(_fakeClient);
    }

    [Fact]
    public async Task PutItemAsync_WithoutCondition_WritesItemToTable()
    {
        var entity = new TestDynamoEntity
        {
            EventId = "evt-001",
            DriverId = "driver-abc",
            Amount = 150.50m,
            ExpiresAt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc)
        };

        var result = await _repository.PutItemAsync(entity);

        Assert.True(result);
        Assert.Single(_fakeClient.PutItemCalls);
        var call = _fakeClient.PutItemCalls[0];
        Assert.Equal("TestEvents", call.TableName);
        Assert.Equal("EVENT#evt-001", call.Item["pk"].S);
        Assert.Equal("EVENT#evt-001", call.Item["sk"].S);
        Assert.Null(call.ConditionExpression);
    }

    [Fact]
    public async Task PutItemAsync_WithConditionalOnNotExists_SetsConditionExpression()
    {
        var entity = new TestDynamoEntity
        {
            EventId = "evt-002",
            DriverId = "driver-xyz",
            Amount = 75.00m,
            ExpiresAt = DateTime.UtcNow.AddDays(90)
        };

        var result = await _repository.PutItemAsync(entity, conditionalOnNotExists: true);

        Assert.True(result);
        Assert.Single(_fakeClient.PutItemCalls);
        var call = _fakeClient.PutItemCalls[0];
        Assert.Equal("attribute_not_exists(pk)", call.ConditionExpression);
    }

    [Fact]
    public async Task PutItemAsync_ConditionalFailure_ReturnsFalse()
    {
        _fakeClient.SimulateConditionalFailure = true;

        var entity = new TestDynamoEntity
        {
            EventId = "evt-duplicate",
            DriverId = "driver-abc",
            Amount = 100.00m,
            ExpiresAt = DateTime.UtcNow.AddDays(90)
        };

        var result = await _repository.PutItemAsync(entity, conditionalOnNotExists: true);

        Assert.False(result);
    }

    [Fact]
    public async Task GetItemAsync_ItemExists_ReturnsEntity()
    {
        _fakeClient.GetItemResponse = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "EVENT#evt-100" },
            ["sk"] = new AttributeValue { S = "EVENT#evt-100" },
            ["eventId"] = new AttributeValue { S = "evt-100" },
            ["driverId"] = new AttributeValue { S = "driver-999" },
            ["amountCentavos"] = new AttributeValue { N = "5000" },
            ["expiresAt"] = new AttributeValue { N = "1750000000" }
        };

        var result = await _repository.GetItemAsync("EVENT#evt-100", "EVENT#evt-100");

        Assert.NotNull(result);
        Assert.Equal("evt-100", result.EventId);
        Assert.Equal("driver-999", result.DriverId);
        Assert.Equal(50.00m, result.Amount);
    }

    [Fact]
    public async Task GetItemAsync_ItemNotFound_ReturnsNull()
    {
        _fakeClient.GetItemResponse = null;

        var result = await _repository.GetItemAsync("EVENT#missing", "EVENT#missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task QueryAsync_WithPartitionKeyOnly_QueriesCorrectly()
    {
        _fakeClient.QueryResponse = new List<Dictionary<string, AttributeValue>>
        {
            new()
            {
                ["pk"] = new AttributeValue { S = "EVENT#evt-200" },
                ["sk"] = new AttributeValue { S = "EVENT#evt-200" },
                ["eventId"] = new AttributeValue { S = "evt-200" },
                ["driverId"] = new AttributeValue { S = "driver-1" },
                ["amountCentavos"] = new AttributeValue { N = "10000" },
                ["expiresAt"] = new AttributeValue { N = "1750000000" }
            }
        };

        var results = await _repository.QueryAsync("EVENT#evt-200");

        Assert.Single(results);
        Assert.Equal("evt-200", results[0].EventId);
        Assert.Single(_fakeClient.QueryCalls);
        var call = _fakeClient.QueryCalls[0];
        Assert.Equal("TestEvents", call.TableName);
        Assert.Contains(":pk", call.ExpressionAttributeValues.Keys);
        Assert.Null(call.IndexName);
    }

    [Fact]
    public async Task QueryAsync_WithSortKeyPrefix_AddsBeginsWithCondition()
    {
        _fakeClient.QueryResponse = new List<Dictionary<string, AttributeValue>>();

        await _repository.QueryAsync("USER#driver-1", skPrefix: "MSG#");

        Assert.Single(_fakeClient.QueryCalls);
        var call = _fakeClient.QueryCalls[0];
        Assert.Contains("begins_with", call.KeyConditionExpression);
        Assert.Contains(":skPrefix", call.ExpressionAttributeValues.Keys);
        Assert.Equal("MSG#", call.ExpressionAttributeValues[":skPrefix"].S);
    }

    [Fact]
    public async Task QueryByIndexAsync_UsesCorrectIndexName()
    {
        _fakeClient.QueryResponse = new List<Dictionary<string, AttributeValue>>();

        await _repository.QueryByIndexAsync("byDriverId", "driver-abc");

        Assert.Single(_fakeClient.QueryCalls);
        var call = _fakeClient.QueryCalls[0];
        Assert.Equal("byDriverId", call.IndexName);
    }

    [Fact]
    public async Task DeleteAsync_SendsCorrectKey()
    {
        await _repository.DeleteAsync("EVENT#evt-300", "EVENT#evt-300");

        Assert.Single(_fakeClient.DeleteItemCalls);
        var call = _fakeClient.DeleteItemCalls[0];
        Assert.Equal("TestEvents", call.TableName);
        Assert.Equal("EVENT#evt-300", call.Key["pk"].S);
        Assert.Equal("EVENT#evt-300", call.Key["sk"].S);
    }

    [Fact]
    public async Task PutItemAsync_IncludesTtlAsEpochSeconds()
    {
        var expiresAt = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var entity = new TestDynamoEntity
        {
            EventId = "evt-ttl",
            DriverId = "driver-ttl",
            Amount = 25.00m,
            ExpiresAt = expiresAt
        };

        await _repository.PutItemAsync(entity);

        var call = _fakeClient.PutItemCalls[0];
        var expectedEpoch = new DateTimeOffset(expiresAt).ToUnixTimeSeconds().ToString();
        Assert.Equal(expectedEpoch, call.Item["expiresAt"].N);
    }
}
