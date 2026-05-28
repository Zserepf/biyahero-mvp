using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BiyaHero.Api.Domain;

/// <summary>
/// Represents a digital-wallet payment event in the Anti-123 system.
/// Inherits BaseDomain; provides Serialize/Parse round-trip target for Property 2.
/// 
/// The Anti-123 pattern uses conditional DynamoDB writes keyed by EventId
/// (webhook-supplied unique identifier) to enforce exactly-once processing.
/// When the target driver is offline, the event is queued in QueuedMessages
/// and drained on reconnect — no confirmed payment is ever silently dropped.
/// 
/// Validates: Requirements 3.1, 3.10, 3.11
/// </summary>
public class PaymentEvent : BaseDomain
{
    /// <summary>
    /// Webhook-supplied unique event identifier used as the DynamoDB partition key
    /// (PK: EVENT#{EventId}). The conditional write (attribute_not_exists) on this
    /// field enforces idempotent webhook processing (Req 3.7).
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Idempotency key for queued message deduplication in the Anti-123 pattern.
    /// Composed from the EventId and DriverId to ensure a payment confirmation
    /// is delivered at most once per driver, even across reconnect/drain cycles.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// The driver who should receive the payment confirmation.
    /// </summary>
    public Guid DriverId { get; set; }

    /// <summary>
    /// The payer (commuter) who initiated the payment.
    /// Named PayerId per the design class diagram (Req 3.1, 3.10).
    /// </summary>
    public Guid PayerId { get; set; }

    /// <summary>
    /// Display name of the payer, shown on the driver's payment dashboard (Req 3.3).
    /// </summary>
    public string PayerName { get; set; } = string.Empty;

    /// <summary>
    /// The route associated with this payment event.
    /// </summary>
    public Guid RouteId { get; set; }

    /// <summary>
    /// Payment amount in centavos (integer representation avoids floating-point issues).
    /// </summary>
    public int AmountCentavos { get; set; }

    /// <summary>
    /// ISO 4217 currency code. Defaults to "PHP" (Philippine Peso).
    /// </summary>
    public string Currency { get; set; } = "PHP";

    /// <summary>
    /// Lifecycle status of this payment event.
    /// </summary>
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>
    /// Name of the digital wallet provider that originated this event
    /// (e.g., "GCash", "Maya", "MockWallet"). Used for audit and routing.
    /// </summary>
    public string WalletProvider { get; set; } = string.Empty;

    /// <summary>
    /// Transaction reference from the wallet provider for audit/reconciliation.
    /// Null until the wallet adapter confirms the transaction.
    /// </summary>
    public string? WalletTransactionId { get; set; }

    /// <summary>
    /// Timestamp when the payment actually occurred at the wallet provider.
    /// Used as the sort key in the byDriverId GSI for chronological ordering.
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// Timestamp from the webhook payload (X-Wallet-Timestamp header).
    /// Checked against a ±5-minute window to block replay attacks (Req 3.5).
    /// </summary>
    public DateTime WebhookTimestamp { get; set; }

    /// <summary>
    /// Timestamp when the webhook was received and signature verified.
    /// Null if the event was created locally before webhook confirmation.
    /// </summary>
    public DateTime? ProcessedTimestamp { get; set; }

    /// <summary>
    /// SHA-256 hash of the raw webhook payload body.
    /// Stored for audit trail and replay detection without retaining the full payload.
    /// </summary>
    public string RawPayloadHash { get; set; } = string.Empty;

    public PaymentEvent() : base()
    {
        OccurredAt = DateTime.UtcNow;
        WebhookTimestamp = DateTime.UtcNow;
    }

    public PaymentEvent(
        Guid id,
        DateTime createdAt,
        DateTime updatedAt,
        string eventId,
        string idempotencyKey,
        Guid driverId,
        Guid payerId,
        string payerName,
        Guid routeId,
        int amountCentavos,
        string currency,
        PaymentStatus status,
        string walletProvider,
        string? walletTransactionId,
        DateTime occurredAt,
        DateTime webhookTimestamp,
        DateTime? processedTimestamp,
        string rawPayloadHash)
        : base(id, createdAt, updatedAt)
    {
        EventId = eventId;
        IdempotencyKey = idempotencyKey;
        DriverId = driverId;
        PayerId = payerId;
        PayerName = payerName;
        RouteId = routeId;
        AmountCentavos = amountCentavos;
        Currency = currency;
        Status = status;
        WalletProvider = walletProvider;
        WalletTransactionId = walletTransactionId;
        OccurredAt = occurredAt;
        WebhookTimestamp = webhookTimestamp;
        ProcessedTimestamp = processedTimestamp;
        RawPayloadHash = rawPayloadHash;
    }

    /// <summary>
    /// Generate the idempotency key from the event ID and driver ID.
    /// This key is used for queued message deduplication in the Anti-123 pattern,
    /// ensuring a payment confirmation is delivered at most once per driver.
    /// </summary>
    public static string GenerateIdempotencyKey(string eventId, Guid driverId)
    {
        return $"{eventId}:{driverId}";
    }

    /// <summary>
    /// Compute a SHA-256 hash of the raw webhook payload body.
    /// Stored for audit trail and replay detection without retaining the full payload.
    /// </summary>
    public static string ComputePayloadHash(string rawPayload)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Serialize this PaymentEvent to a JSON-compatible dictionary.
    /// Includes all base properties plus payment-specific fields.
    /// Round-trip safe: Parse(Serialize(x)) == x for Property 2.
    /// </summary>
    public override Dictionary<string, object?> Serialize()
    {
        var dict = base.Serialize();
        dict["eventId"] = EventId;
        dict["idempotencyKey"] = IdempotencyKey;
        dict["driverId"] = DriverId.ToString();
        dict["payerId"] = PayerId.ToString();
        dict["payerName"] = PayerName;
        dict["routeId"] = RouteId.ToString();
        dict["amountCentavos"] = AmountCentavos;
        dict["currency"] = Currency;
        dict["status"] = Status.ToString();
        dict["walletProvider"] = WalletProvider;
        dict["walletTransactionId"] = WalletTransactionId;
        dict["occurredAt"] = OccurredAt.ToString("o");
        dict["webhookTimestamp"] = WebhookTimestamp.ToString("o");
        dict["processedTimestamp"] = ProcessedTimestamp?.ToString("o");
        dict["rawPayloadHash"] = RawPayloadHash;
        return dict;
    }

    /// <summary>
    /// Parse a serialized dictionary back into a PaymentEvent instance.
    /// This is the inverse of Serialize() and enables round-trip verification (Property 2).
    /// Uses RoundtripKind to preserve DateTime UTC kind through serialization.
    /// </summary>
    public static PaymentEvent Parse(Dictionary<string, object?> data)
    {
        var id = Guid.Parse(data["id"]?.ToString() ?? throw new ArgumentException("Missing id"));
        var createdAt = DateTime.Parse(
            data["createdAt"]?.ToString() ?? throw new ArgumentException("Missing createdAt"),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var updatedAt = DateTime.Parse(
            data["updatedAt"]?.ToString() ?? throw new ArgumentException("Missing updatedAt"),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var eventId = data["eventId"]?.ToString() ?? throw new ArgumentException("Missing eventId");
        var idempotencyKey = data["idempotencyKey"]?.ToString() ?? throw new ArgumentException("Missing idempotencyKey");
        var driverId = Guid.Parse(data["driverId"]?.ToString() ?? throw new ArgumentException("Missing driverId"));
        var payerId = Guid.Parse(data["payerId"]?.ToString() ?? throw new ArgumentException("Missing payerId"));
        var payerName = data["payerName"]?.ToString() ?? throw new ArgumentException("Missing payerName");
        var routeId = Guid.Parse(data["routeId"]?.ToString() ?? throw new ArgumentException("Missing routeId"));
        var amountCentavos = int.Parse(data["amountCentavos"]?.ToString() ?? throw new ArgumentException("Missing amountCentavos"));
        var currency = data["currency"]?.ToString() ?? throw new ArgumentException("Missing currency");
        var status = Enum.Parse<PaymentStatus>(data["status"]?.ToString() ?? throw new ArgumentException("Missing status"));
        var walletProvider = data["walletProvider"]?.ToString() ?? throw new ArgumentException("Missing walletProvider");
        var walletTransactionId = data.ContainsKey("walletTransactionId") ? data["walletTransactionId"]?.ToString() : null;
        var occurredAt = DateTime.Parse(
            data["occurredAt"]?.ToString() ?? throw new ArgumentException("Missing occurredAt"),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var webhookTimestamp = DateTime.Parse(
            data["webhookTimestamp"]?.ToString() ?? throw new ArgumentException("Missing webhookTimestamp"),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var processedTimestamp = data.ContainsKey("processedTimestamp") && data["processedTimestamp"] != null
            ? DateTime.Parse(data["processedTimestamp"]!.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : (DateTime?)null;
        var rawPayloadHash = data["rawPayloadHash"]?.ToString() ?? throw new ArgumentException("Missing rawPayloadHash");

        return new PaymentEvent(
            id, createdAt, updatedAt,
            eventId, idempotencyKey,
            driverId, payerId, payerName, routeId,
            amountCentavos, currency, status,
            walletProvider, walletTransactionId,
            occurredAt, webhookTimestamp, processedTimestamp,
            rawPayloadHash);
    }
}
