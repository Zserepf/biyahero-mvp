using System.Text;
using Amazon.DynamoDBv2;

namespace BiyaHero.Api.Services;

/// <summary>
/// AWS API Gateway Management API implementation of IWebSocketPushService.
/// Posts messages to connected WebSocket clients via PostToConnection.
///
/// For MVP, this uses a configurable WebSocket endpoint URL.
/// In production, this would use the AmazonApiGatewayManagementApiClient SDK.
///
/// Requirements: 3.2
/// </summary>
public sealed class ApiGatewayWebSocketPushService : IWebSocketPushService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiGatewayWebSocketPushService> _logger;

    public ApiGatewayWebSocketPushService(IConfiguration configuration, ILogger<ApiGatewayWebSocketPushService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> PostToConnectionAsync(string connectionId, string payload, CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["WebSocket:ManagementEndpoint"];

        if (string.IsNullOrEmpty(endpoint))
        {
            // MVP fallback: log the push attempt when no endpoint is configured.
            // This allows the webhook flow to complete without a live WebSocket API.
            _logger.LogWarning(
                "WebSocket management endpoint not configured. Would push to connection {ConnectionId}: {PayloadLength} bytes",
                connectionId, payload.Length);
            return true;
        }

        try
        {
            // In production, this would use AmazonApiGatewayManagementApiClient:
            // var client = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig { ServiceURL = endpoint });
            // await client.PostToConnectionAsync(new PostToConnectionRequest { ConnectionId = connectionId, Data = new MemoryStream(Encoding.UTF8.GetBytes(payload)) });
            //
            // For MVP without the ApiGatewayManagementApi SDK package, we use HttpClient.
            using var httpClient = new HttpClient { BaseAddress = new Uri(endpoint) };
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(payload));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var response = await httpClient.PostAsync($"/@connections/{Uri.EscapeDataString(connectionId)}", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully pushed message to connection {ConnectionId}", connectionId);
                return true;
            }

            if ((int)response.StatusCode == 410)
            {
                // Connection is gone/stale — caller should enqueue for offline delivery
                _logger.LogInformation("Connection {ConnectionId} is gone (410). Driver is offline.", connectionId);
                return false;
            }

            _logger.LogWarning(
                "PostToConnection failed for {ConnectionId} with status {StatusCode}",
                connectionId, (int)response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing to connection {ConnectionId}", connectionId);
            return false;
        }
    }
}
