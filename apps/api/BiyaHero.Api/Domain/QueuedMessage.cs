namespace BiyaHero.Api.Domain;

/// <summary>
/// Represents a payment notification queued for an offline driver.
/// Stored in DynamoDB with PK = USER#{driverId}, SK = MSG#{occurredAt}#{eventId}.
/// Carries a 24-hour TTL; messages are drained in chronological order
/// when the driver reconnects ($connect).
/// Requirement: 3.6
/// </summary>
public class QueuedMessage : BaseDomain
{
    /// <summary>The driver who should receive this message.</summary>
    public Guid DriverId { get; set; }

    /// <summary>The payment event ID that triggered this queued message.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>When the payment event originally occurred.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>The serialized payment notification payload (JSON string).</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>TTL expiry — 24 hours after enqueue time.</summary>
    public DateTime ExpiresAt { get; set; }

    public QueuedMessage() : base() { }

    public QueuedMessage(
        Guid id,
        DateTime createdAt,
        DateTime updatedAt,
        Guid driverId,
        string eventId,
        DateTime occurredAt,
        string payload,
        DateTime expiresAt)
        : base(id, createdAt, updatedAt)
    {
        DriverId = driverId;
        EventId = eventId;
        OccurredAt = occurredAt;
        Payload = payload;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Serialize this QueuedMessage to a JSON-compatible dictionary.
    /// </summary>
    public override Dictionary<string, object?> Serialize()
    {
        var dict = base.Serialize();
        dict["driverId"] = DriverId.ToString();
        dict["eventId"] = EventId;
        dict["occurredAt"] = OccurredAt.ToString("o");
        dict["payload"] = Payload;
        dict["expiresAt"] = ExpiresAt.ToString("o");
        return dict;
    }

    /// <summary>
    /// Parse a serialized dictionary back into a QueuedMessage instance.
    /// Inverse of Serialize() for round-trip verification.
    /// </summary>
    public static QueuedMessage Parse(Dictionary<string, object?> data)
    {
        var id = Guid.Parse(data["id"]?.ToString() ?? throw new ArgumentException("Missing id"));
        var createdAt = DateTime.Parse(data["createdAt"]?.ToString() ?? throw new ArgumentException("Missing createdAt"));
        var updatedAt = DateTime.Parse(data["updatedAt"]?.ToString() ?? throw new ArgumentException("Missing updatedAt"));
        var driverId = Guid.Parse(data["driverId"]?.ToString() ?? throw new ArgumentException("Missing driverId"));
        var eventId = data["eventId"]?.ToString() ?? throw new ArgumentException("Missing eventId");
        var occurredAt = DateTime.Parse(data["occurredAt"]?.ToString() ?? throw new ArgumentException("Missing occurredAt"));
        var payload = data["payload"]?.ToString() ?? throw new ArgumentException("Missing payload");
        var expiresAt = DateTime.Parse(data["expiresAt"]?.ToString() ?? throw new ArgumentException("Missing expiresAt"));

        return new QueuedMessage(id, createdAt, updatedAt, driverId, eventId, occurredAt, payload, expiresAt);
    }
}
