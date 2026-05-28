using System.Text.Json.Serialization;

namespace BiyaHero.Api.WebSockets;

/// <summary>
/// Standard WebSocket message envelope used for all client↔server communication.
/// Client-sent messages: { action, requestId, data }
/// Server-pushed messages: { action, requestId, data, emittedAt }
/// </summary>
public sealed class WsEnvelope
{
    /// <summary>
    /// The route key identifying the message type (e.g., "demand-ping", "subscribe-heatmap", "payment.confirmed").
    /// </summary>
    [JsonPropertyName("action")]
    public required string Action { get; init; }

    /// <summary>
    /// A client-generated UUID correlating request/response pairs.
    /// </summary>
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    /// <summary>
    /// The message payload. Structure varies by action.
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; init; }

    /// <summary>
    /// ISO 8601 timestamp added by the server on pushed messages. Null for client-sent messages.
    /// </summary>
    [JsonPropertyName("emittedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmittedAt { get; init; }
}

/// <summary>
/// Typed envelope for deserialization of incoming client messages where data is a raw JSON element.
/// </summary>
public sealed class WsIncomingEnvelope
{
    [JsonPropertyName("action")]
    public string? Action { get; init; }

    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    [JsonPropertyName("data")]
    public System.Text.Json.JsonElement? Data { get; init; }
}
