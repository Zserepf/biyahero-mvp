using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BiyaHero.Api.Repositories;
using Dapper;
using System.Data;

namespace BiyaHero.Api.Features.Health;

/// <summary>
/// Handles the /v1/health endpoint by checking per-dependency status.
/// Always returns HTTP 200 — the response body indicates overall and per-dependency health.
///
/// Requirements: 7.6
/// </summary>
public sealed class HealthCheckHandler
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HealthCheckHandler> _logger;

    public HealthCheckHandler(
        IDbConnectionFactory connectionFactory,
        IAmazonDynamoDB dynamoDb,
        IConfiguration configuration,
        ILogger<HealthCheckHandler> logger)
    {
        _connectionFactory = connectionFactory;
        _dynamoDb = dynamoDb;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResponse> HandleAsync()
    {
        var postgresStatus = await CheckPostgresAsync();
        var dynamoDbStatus = await CheckDynamoDbAsync();
        var websocketStatus = CheckWebSocketFanOut();

        var overallStatus = (postgresStatus == "healthy" && dynamoDbStatus == "healthy")
            ? "healthy"
            : "degraded";

        return new HealthCheckResponse(
            overallStatus,
            DateTimeOffset.UtcNow,
            new DependencyStatus(postgresStatus, dynamoDbStatus, websocketStatus));
    }

    private async Task<string> CheckPostgresAsync()
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var result = await ((IDbConnection)connection).ExecuteScalarAsync<int>("SELECT 1");
            return result == 1 ? "healthy" : "unhealthy";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Postgres health check failed");
            return "unhealthy";
        }
    }

    private async Task<string> CheckDynamoDbAsync()
    {
        try
        {
            var request = new DescribeTableRequest { TableName = "DemandPings" };
            var response = await _dynamoDb.DescribeTableAsync(request);
            return response.Table?.TableStatus == TableStatus.ACTIVE ? "healthy" : "unhealthy";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DynamoDB health check failed");
            return "unhealthy";
        }
    }

    private string CheckWebSocketFanOut()
    {
        var endpoint = _configuration["WebSocket:ManagementEndpoint"];
        return string.IsNullOrEmpty(endpoint) ? "not_configured" : "healthy";
    }
}
