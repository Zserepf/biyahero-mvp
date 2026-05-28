using System.Text.Json;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Payment.Webhook;

/// <summary>
/// Business logic for POST /v1/payments/webhook.
///
/// Flow:
/// 1. Verify HMAC-SHA256 signature (X-Wallet-Signature) over raw body — reject 401 if invalid/missing
/// 2. Check X-Wallet-Timestamp within ±5 minutes — reject 401 if outside window (replay protection)
/// 3. Conditional persist: attribute_not_exists enforces idempotence — duplicate eventId returns 200 no-op
/// 4. Look up driver in WsConnections — if connected, push payment.confirmed envelope via PostToConnection
/// 5. If driver offline, enqueue in QueuedMessages with 24h TTL
/// 6. Return 200 on success
///
/// Requirements: 3.1, 3.2, 3.5, 3.6, 3.7
/// </summary>
public sealed class WebhookHandler
{
    private readonly IWebhookSignatureVerifier _signatureVerifier;
    private readonly IPaymentEventRepository _paymentEventRepository;
    private readonly IWsConnectionRepository _wsConnectionRepository;
    private readonly IWebSocketPushService _webSocketPushService;
    private readonly IQueuedMessageRepository _queuedMessageRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookHandler> _logger;

    public WebhookHandler(
        IWebhookSignatureVerifier signatureVerifier,
        IPaymentEventRepository paymentEventRepository,
        IWsConnectionRepository wsConnectionRepository,
        IWebSocketPushService webSocketPushService,
        IQueuedMessageRepository queuedMessageRepository,
        TimeProvider timeProvider,
        ILogger<WebhookHandler> logger)
    {
        _signatureVerifier = signatureVerifier;
        _paymentEventRepository = paymentEventRepository;
        _wsConnectionRepository = wsConnectionRepository;
        _webSocketPushService = webSocketPushService;
        _queuedMessageRepository = queuedMessageRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Processes the webhook request.
    /// Returns a WebhookHandlerResult indicating the outcome.
    /// </summary>
    public async Task<WebhookHandlerResult> HandleAsync(
        byte[] rawBody,
        string? signature,
        string? timestamp,
        CancellationToken cancellationToken = default)
    {
        // Step 1 & 2: Verify signature and timestamp window
        var verificationResult = await _signatureVerifier.VerifyAsync(
            rawBody, signature, timestamp, cancellationToken);

        if (!verificationResult.IsValid)
        {
            _logger.LogWarning("Webhook signature verification failed: {Reason}", verificationResult.Reason);
            return WebhookHandlerResult.Unauthorized(verificationResult.Reason ?? "Invalid signature.");
        }

        // Parse the webhook payload
        WebhookRequest? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WebhookRequest>(rawBody, WebhookJsonContext.Default.WebhookRequest);
        }
        catch (JsonException)
        {
            return WebhookHandlerResult.Unauthorized("Malformed webhook payload.");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.EventId))
        {
            return WebhookHandlerResult.Unauthorized("Missing eventId in webhook payload.");
        }

        // Step 3: Build PaymentEvent and attempt conditional persist
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var driverId = Guid.TryParse(payload.DriverId, out var parsedDriverId) ? parsedDriverId : Guid.Empty;
        var rawPayloadHash = PaymentEvent.ComputePayloadHash(
            System.Text.Encoding.UTF8.GetString(rawBody));
        var webhookTs = DateTime.TryParse(timestamp, out var parsedTs) ? parsedTs.ToUniversalTime() : now;

        var paymentEvent = new PaymentEvent
        {
            Id = Guid.TryParse(payload.EventId, out var parsedId) ? parsedId : Guid.NewGuid(),
            EventId = payload.EventId,
            IdempotencyKey = PaymentEvent.GenerateIdempotencyKey(payload.EventId, driverId),
            DriverId = driverId,
            PayerId = Guid.TryParse(payload.PayerId, out var payerId) ? payerId : Guid.Empty,
            PayerName = payload.PayerName ?? string.Empty,
            RouteId = Guid.TryParse(payload.RouteId, out var routeId) ? routeId : Guid.Empty,
            AmountCentavos = payload.AmountCentavos ?? 0,
            Currency = payload.Currency ?? "PHP",
            Status = PaymentStatus.Confirmed,
            WalletProvider = payload.WalletProvider ?? "MockWallet",
            WalletTransactionId = payload.WalletTransactionId,
            OccurredAt = payload.OccurredAt ?? now,
            WebhookTimestamp = webhookTs,
            ProcessedTimestamp = now,
            RawPayloadHash = rawPayloadHash,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Conditional persist: attribute_not_exists enforces idempotence (Req 3.7)
        // Returns false if the event already exists — duplicate returns 200 with no side effects
        var isNew = await _paymentEventRepository.PutEventAsync(paymentEvent, cancellationToken);

        if (!isNew)
        {
            _logger.LogInformation("Duplicate webhook eventId={EventId} — idempotent no-op", payload.EventId);
            return WebhookHandlerResult.Success();
        }

        // Step 4 & 5: Fan-out to driver — push if online, enqueue if offline
        await DeliverToDriverAsync(paymentEvent, cancellationToken);

        return WebhookHandlerResult.Success();
    }

    /// <summary>
    /// Looks up the driver in WsConnections. If connected, pushes the payment.confirmed
    /// envelope via PostToConnection. If offline (no connections or push fails),
    /// enqueues in QueuedMessages with 24h TTL.
    /// Requirements: 3.2, 3.6
    /// </summary>
    private async Task DeliverToDriverAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken)
    {
        var envelope = BuildPaymentConfirmedEnvelope(paymentEvent);

        // Look up driver's active WebSocket connections
        var connections = await _wsConnectionRepository.GetConnectionsByUserAsync(
            paymentEvent.DriverId, cancellationToken);

        if (connections.Count == 0)
        {
            // Driver is offline — enqueue for delivery on reconnect (Req 3.6)
            _logger.LogInformation(
                "Driver {DriverId} is offline. Enqueuing payment notification for eventId={EventId}",
                paymentEvent.DriverId, paymentEvent.EventId);

            await _queuedMessageRepository.EnqueueAsync(
                paymentEvent.DriverId,
                paymentEvent.EventId,
                paymentEvent.OccurredAt,
                envelope,
                cancellationToken);
            return;
        }

        // Driver is connected — push to all active connections
        var delivered = false;
        foreach (var connection in connections)
        {
            var success = await _webSocketPushService.PostToConnectionAsync(
                connection.ConnectionId, envelope, cancellationToken);

            if (success)
            {
                delivered = true;
                _logger.LogInformation(
                    "Pushed payment.confirmed to driver {DriverId} via connection {ConnectionId}",
                    paymentEvent.DriverId, connection.ConnectionId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to push to connection {ConnectionId} for driver {DriverId} — connection may be stale",
                    connection.ConnectionId, paymentEvent.DriverId);
            }
        }

        // If all push attempts failed, enqueue as fallback (Req 3.6)
        if (!delivered)
        {
            _logger.LogInformation(
                "All push attempts failed for driver {DriverId}. Enqueuing payment notification for eventId={EventId}",
                paymentEvent.DriverId, paymentEvent.EventId);

            await _queuedMessageRepository.EnqueueAsync(
                paymentEvent.DriverId,
                paymentEvent.EventId,
                paymentEvent.OccurredAt,
                envelope,
                cancellationToken);
        }
    }

    /// <summary>
    /// Builds the payment.confirmed WebSocket envelope per the design spec:
    /// { "action": "payment.confirmed", "requestId": "...", "data": { ... }, "emittedAt": "..." }
    /// </summary>
    private string BuildPaymentConfirmedEnvelope(PaymentEvent paymentEvent)
    {
        var envelope = new Dictionary<string, object>
        {
            ["action"] = "payment.confirmed",
            ["requestId"] = Guid.NewGuid().ToString(),
            ["data"] = new Dictionary<string, object?>
            {
                ["eventId"] = paymentEvent.EventId,
                ["driverId"] = paymentEvent.DriverId.ToString(),
                ["payerId"] = paymentEvent.PayerId.ToString(),
                ["payerName"] = paymentEvent.PayerName,
                ["routeId"] = paymentEvent.RouteId.ToString(),
                ["amountCentavos"] = paymentEvent.AmountCentavos,
                ["currency"] = paymentEvent.Currency,
                ["occurredAt"] = paymentEvent.OccurredAt.ToString("o")
            },
            ["emittedAt"] = _timeProvider.GetUtcNow().ToString("o")
        };

        return JsonSerializer.Serialize(envelope);
    }
}
