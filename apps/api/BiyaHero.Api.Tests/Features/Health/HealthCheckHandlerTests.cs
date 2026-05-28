using System.Data;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using BiyaHero.Api.Features.Health;
using BiyaHero.Api.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BiyaHero.Api.Tests.Features.Health;

public class HealthCheckHandlerTests
{
    private readonly NullLogger<HealthCheckHandler> _logger = new();

    [Fact]
    public async Task HandleAsync_AllHealthy_ReturnsHealthyStatus()
    {
        var handler = CreateHandler(
            postgresHealthy: true,
            dynamoDbHealthy: true,
            webSocketConfigured: true);

        var result = await handler.HandleAsync();

        Assert.Equal("healthy", result.Status);
        Assert.Equal("healthy", result.Dependencies.Postgres);
        Assert.Equal("healthy", result.Dependencies.Dynamodb);
        Assert.Equal("healthy", result.Dependencies.Websocket);
        Assert.True(result.Timestamp <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task HandleAsync_PostgresUnhealthy_ReturnsDegradedStatus()
    {
        var handler = CreateHandler(
            postgresHealthy: false,
            dynamoDbHealthy: true,
            webSocketConfigured: true);

        var result = await handler.HandleAsync();

        Assert.Equal("degraded", result.Status);
        Assert.Equal("unhealthy", result.Dependencies.Postgres);
        Assert.Equal("healthy", result.Dependencies.Dynamodb);
    }

    [Fact]
    public async Task HandleAsync_DynamoDbUnhealthy_ReturnsDegradedStatus()
    {
        var handler = CreateHandler(
            postgresHealthy: true,
            dynamoDbHealthy: false,
            webSocketConfigured: true);

        var result = await handler.HandleAsync();

        Assert.Equal("degraded", result.Status);
        Assert.Equal("healthy", result.Dependencies.Postgres);
        Assert.Equal("unhealthy", result.Dependencies.Dynamodb);
    }

    [Fact]
    public async Task HandleAsync_WebSocketNotConfigured_ReportsNotConfigured()
    {
        var handler = CreateHandler(
            postgresHealthy: true,
            dynamoDbHealthy: true,
            webSocketConfigured: false);

        var result = await handler.HandleAsync();

        // WebSocket not_configured does not degrade overall status
        Assert.Equal("healthy", result.Status);
        Assert.Equal("not_configured", result.Dependencies.Websocket);
    }

    [Fact]
    public async Task HandleAsync_AllUnhealthy_ReturnsDegradedStatus()
    {
        var handler = CreateHandler(
            postgresHealthy: false,
            dynamoDbHealthy: false,
            webSocketConfigured: false);

        var result = await handler.HandleAsync();

        Assert.Equal("degraded", result.Status);
        Assert.Equal("unhealthy", result.Dependencies.Postgres);
        Assert.Equal("unhealthy", result.Dependencies.Dynamodb);
        Assert.Equal("not_configured", result.Dependencies.Websocket);
    }

    private HealthCheckHandler CreateHandler(
        bool postgresHealthy,
        bool dynamoDbHealthy,
        bool webSocketConfigured)
    {
        var connectionFactory = new FakeDbConnectionFactory(postgresHealthy);
        var dynamoDb = new FakeDynamoDb(dynamoDbHealthy);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(webSocketConfigured
                ? new Dictionary<string, string?> { ["WebSocket:ManagementEndpoint"] = "https://ws.example.com" }
                : new Dictionary<string, string?>())
            .Build();

        return new HealthCheckHandler(connectionFactory, dynamoDb, config, _logger);
    }

    // ─── Fakes ──────────────────────────────────────────────────────────

    private sealed class FakeDbConnectionFactory : IDbConnectionFactory
    {
        private readonly bool _healthy;

        public FakeDbConnectionFactory(bool healthy) => _healthy = healthy;

        public Task<IDbConnection> CreateConnectionAsync()
        {
            if (!_healthy)
                throw new Exception("Postgres connection failed");

            return Task.FromResult<IDbConnection>(new FakeDbConnection());
        }
    }

    private sealed class FakeDbConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = "";
        public int ConnectionTimeout => 30;
        public string Database => "test";
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotImplementedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand()
        {
            return new FakeDbCommand();
        }
        public void Dispose() { }
        public void Open() { }
    }

    private sealed class FakeDbCommand : IDbCommand
    {
        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters => new FakeParameterCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => throw new NotImplementedException();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotImplementedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotImplementedException();
        public object ExecuteScalar() => 1; // SELECT 1 returns 1
        public void Prepare() { }
    }

    private sealed class FakeParameterCollection : IDataParameterCollection
    {
        private readonly List<object> _items = new();
        public object this[string parameterName] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public object this[int index] { get => _items[index]; set => _items[index] = value; }
        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public bool IsSynchronized => false;
        public int Count => _items.Count;
        public object SyncRoot => _items;
        public int Add(object value) { _items.Add(value); return _items.Count - 1; }
        public void Clear() => _items.Clear();
        public bool Contains(string parameterName) => false;
        public bool Contains(object value) => _items.Contains(value);
        public void CopyTo(Array array, int index) { }
        public System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
        public int IndexOf(string parameterName) => -1;
        public int IndexOf(object value) => _items.IndexOf(value);
        public void Insert(int index, object value) => _items.Insert(index, value);
        public void Remove(object value) => _items.Remove(value);
        public void RemoveAt(string parameterName) { }
        public void RemoveAt(int index) => _items.RemoveAt(index);
    }

    private sealed class FakeDynamoDb : IAmazonDynamoDB
    {
        private readonly bool _healthy;

        public FakeDynamoDb(bool healthy) => _healthy = healthy;

        public IClientConfig Config => throw new NotImplementedException();

        public Task<DescribeTableResponse> DescribeTableAsync(string tableName, CancellationToken cancellationToken = default)
        {
            return DescribeTableAsync(new DescribeTableRequest { TableName = tableName }, cancellationToken);
        }

        public Task<DescribeTableResponse> DescribeTableAsync(DescribeTableRequest request, CancellationToken cancellationToken = default)
        {
            if (!_healthy)
                throw new Exception("DynamoDB unavailable");

            return Task.FromResult(new DescribeTableResponse
            {
                Table = new TableDescription { TableStatus = TableStatus.ACTIVE }
            });
        }

        // All other IAmazonDynamoDB members throw NotImplementedException
        public void Dispose() { }
        public Task<BatchExecuteStatementResponse> BatchExecuteStatementAsync(BatchExecuteStatementRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<BatchGetItemResponse> BatchGetItemAsync(BatchGetItemRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<BatchGetItemResponse> BatchGetItemAsync(Dictionary<string, KeysAndAttributes> requestItems, ReturnConsumedCapacity returnConsumedCapacity, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<BatchGetItemResponse> BatchGetItemAsync(Dictionary<string, KeysAndAttributes> requestItems, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<BatchWriteItemResponse> BatchWriteItemAsync(Dictionary<string, List<WriteRequest>> requestItems, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<BatchWriteItemResponse> BatchWriteItemAsync(BatchWriteItemRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CreateBackupResponse> CreateBackupAsync(CreateBackupRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CreateGlobalTableResponse> CreateGlobalTableAsync(CreateGlobalTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CreateTableResponse> CreateTableAsync(CreateTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CreateTableResponse> CreateTableAsync(string tableName, List<KeySchemaElement> keySchema, List<AttributeDefinition> attributeDefinitions, ProvisionedThroughput provisionedThroughput, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DeleteBackupResponse> DeleteBackupAsync(DeleteBackupRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DeleteItemResponse> DeleteItemAsync(string tableName, Dictionary<string, AttributeValue> key, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DeleteItemResponse> DeleteItemAsync(string tableName, Dictionary<string, AttributeValue> key, ReturnValue returnValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DeleteItemResponse> DeleteItemAsync(DeleteItemRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DeleteResourcePolicyResponse> DeleteResourcePolicyAsync(DeleteResourcePolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DeleteTableResponse> DeleteTableAsync(string tableName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DeleteTableResponse> DeleteTableAsync(DeleteTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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
        public Task<DescribeTableReplicaAutoScalingResponse> DescribeTableReplicaAutoScalingAsync(DescribeTableReplicaAutoScalingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DescribeTimeToLiveResponse> DescribeTimeToLiveAsync(string tableName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DescribeTimeToLiveResponse> DescribeTimeToLiveAsync(DescribeTimeToLiveRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DisableKinesisStreamingDestinationResponse> DisableKinesisStreamingDestinationAsync(DisableKinesisStreamingDestinationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<EnableKinesisStreamingDestinationResponse> EnableKinesisStreamingDestinationAsync(EnableKinesisStreamingDestinationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ExecuteStatementResponse> ExecuteStatementAsync(ExecuteStatementRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ExecuteTransactionResponse> ExecuteTransactionAsync(ExecuteTransactionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ExportTableToPointInTimeResponse> ExportTableToPointInTimeAsync(ExportTableToPointInTimeRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GetItemResponse> GetItemAsync(string tableName, Dictionary<string, AttributeValue> key, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GetItemResponse> GetItemAsync(string tableName, Dictionary<string, AttributeValue> key, bool consistentRead, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GetItemResponse> GetItemAsync(GetItemRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GetResourcePolicyResponse> GetResourcePolicyAsync(GetResourcePolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ImportTableResponse> ImportTableAsync(ImportTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ListBackupsResponse> ListBackupsAsync(ListBackupsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ListContributorInsightsResponse> ListContributorInsightsAsync(ListContributorInsightsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ListExportsResponse> ListExportsAsync(ListExportsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ListGlobalTablesResponse> ListGlobalTablesAsync(ListGlobalTablesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ListImportsResponse> ListImportsAsync(ListImportsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ListTablesResponse> ListTablesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ListTablesResponse> ListTablesAsync(string exclusiveStartTableName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ListTablesResponse> ListTablesAsync(string exclusiveStartTableName, int limit, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ListTablesResponse> ListTablesAsync(int limit, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ListTablesResponse> ListTablesAsync(ListTablesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ListTagsOfResourceResponse> ListTagsOfResourceAsync(ListTagsOfResourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PutItemResponse> PutItemAsync(string tableName, Dictionary<string, AttributeValue> item, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PutItemResponse> PutItemAsync(string tableName, Dictionary<string, AttributeValue> item, ReturnValue returnValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PutItemResponse> PutItemAsync(PutItemRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PutResourcePolicyResponse> PutResourcePolicyAsync(PutResourcePolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RestoreTableFromBackupResponse> RestoreTableFromBackupAsync(RestoreTableFromBackupRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RestoreTableToPointInTimeResponse> RestoreTableToPointInTimeAsync(RestoreTableToPointInTimeRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ScanResponse> ScanAsync(string tableName, List<string> attributesToGet, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ScanResponse> ScanAsync(string tableName, Dictionary<string, Condition> scanFilter, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ScanResponse> ScanAsync(string tableName, List<string> attributesToGet, Dictionary<string, Condition> scanFilter, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ScanResponse> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TagResourceResponse> TagResourceAsync(TagResourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UntagResourceResponse> UntagResourceAsync(UntagResourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UpdateContinuousBackupsResponse> UpdateContinuousBackupsAsync(UpdateContinuousBackupsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UpdateContributorInsightsResponse> UpdateContributorInsightsAsync(UpdateContributorInsightsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UpdateGlobalTableResponse> UpdateGlobalTableAsync(UpdateGlobalTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UpdateGlobalTableSettingsResponse> UpdateGlobalTableSettingsAsync(UpdateGlobalTableSettingsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UpdateItemResponse> UpdateItemAsync(string tableName, Dictionary<string, AttributeValue> key, Dictionary<string, AttributeValueUpdate> attributeUpdates, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UpdateItemResponse> UpdateItemAsync(string tableName, Dictionary<string, AttributeValue> key, Dictionary<string, AttributeValueUpdate> attributeUpdates, ReturnValue returnValues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UpdateItemResponse> UpdateItemAsync(UpdateItemRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UpdateKinesisStreamingDestinationResponse> UpdateKinesisStreamingDestinationAsync(UpdateKinesisStreamingDestinationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UpdateTableResponse> UpdateTableAsync(string tableName, ProvisionedThroughput provisionedThroughput, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UpdateTableResponse> UpdateTableAsync(UpdateTableRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UpdateTableReplicaAutoScalingResponse> UpdateTableReplicaAutoScalingAsync(UpdateTableReplicaAutoScalingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UpdateTimeToLiveResponse> UpdateTimeToLiveAsync(UpdateTimeToLiveRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TransactGetItemsResponse> TransactGetItemsAsync(TransactGetItemsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TransactWriteItemsResponse> TransactWriteItemsAsync(TransactWriteItemsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Amazon.Runtime.Endpoints.Endpoint DetermineServiceOperationEndpoint(AmazonWebServiceRequest request) => throw new NotImplementedException();
        public IDynamoDBv2PaginatorFactory Paginators => throw new NotImplementedException();
    }
}
