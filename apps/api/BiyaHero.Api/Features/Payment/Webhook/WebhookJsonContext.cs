using System.Text.Json.Serialization;

namespace BiyaHero.Api.Features.Payment.Webhook;

/// <summary>
/// AOT-compatible JSON serializer context for webhook payload deserialization.
/// </summary>
[JsonSerializable(typeof(WebhookRequest))]
[JsonSerializable(typeof(WebhookResponse))]
internal partial class WebhookJsonContext : JsonSerializerContext
{
}
