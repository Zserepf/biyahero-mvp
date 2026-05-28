using System.Text;
using System.Text.Json;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Payment;
using BiyaHero.Api.Features.Payment.Webhook;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BiyaHero.Api.Tests.Features.Payment;

/// <summary>
/// Unit tests for WebhookHandler covering:
/// - Signature verification rejection (Req 3.5)
/// - Idempotent duplicate handling (Req 3.7)
/// - Online driver push via PostToConnection (Req 3.2)
/// - Offline driver enqueue in QueuedMessages (Req 3.6)
/// - Fallback to enqueue when push fails
/// </summary>
public class WebhookHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeSignatureVerifier _signatureVerifier = new();
    private readonly FakePaymentEventRepository _paymentEventRepository = new();
    private readonly FakeWsConnectionRepository _wsConnectionRepository = new();
    private readonly FakeWebSocketPushService _webSocketPushService = new();
    private readonly FakeQueuedMessageRepository _queuedMessageRepository = new();
    private readonly FakeTimeProvider _timeProvider = new(FixedNow);
    private readonly WebhookHandler _handler;

    public WebhookHandlerTests()
    {
        _handler = new WebhookHandler(
            _signatureVerifier,
            _paymentEventRepository,
            _wsConnectionRepository,
            _webSocketPushService,
            _queuedMessageRepository,
            _timeProvider,
            NullLogger<WebhookHandler>.Instance);
    }

    // ─── Signature Verification (Req 3.5) ─────────────────────────────────

    [Fact]
    public async Task HandleAsync_InvalidSignature_ReturnsUnauthorized_NoFanOut()
    {
        _signatureVerifier.SetResult(WebhookSignatureResult.Invalid("Bad signature"));
        var body = BuildPayload("evt-001", Guid.NewGuid());

        var result = await _handler.HandleAsync(body, "bad-sig", FixedNow.ToString("o"));

        Assert.True(result.IsUnauthorized);
        Assert.Equal("Bad signature", result.ErrorMessage);
        Assert.Empty(_paymentEventRepository.StoredEvents);
        Assert.Empty(_queuedMessageRepository.EnqueuedMessages);
        Assert.Empty(_webSocketPushService.PushedMessages);
    }

    [Fact]
    public async Task HandleAsync_MissingSignature_ReturnsUnauthorized()
    {
        _signatureVerifier.SetResult(WebhookSignatureResult.Invalid("Missing X-Wallet-Signature header."));
        var body = BuildPayload("evt-002", Guid.NewGuid());

        var result = await _handler.HandleAsync(body, null, FixedNow.ToString("o"));

        Assert.True(result.IsUnauthorized);
        Assert.Contains("Missing", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_TimestampOutsideWindow_ReturnsUnauthorized()
    {
        _signatureVerifier.SetResult(WebhookSignatureResult.Invalid("X-Wallet-Timestamp is outside the ±5 minute tolerance window."));
        var body = BuildPayload("evt-003", Guid.NewGuid());

        var result = await _handler.HandleAsync(body, "sig", FixedNow.AddMinutes(-10).ToString("o"));

        Assert.True(result.IsUnauthorized);
        Assert.Contains("tolerance", result.ErrorMessage);
    }

    // ─── Malformed Payload ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_MalformedJson_ReturnsUnauthorized()
    {
        _signatureVerifier.SetResult(WebhookSignatureResult.Valid());
        var body = Encoding.UTF8.GetBytes("not valid json {{{");

        var result = await _handler.HandleAsync(body, "sig", FixedNow.ToString("o"));

        Assert.True(result.IsUnauthorized);
        Assert.Contains("Malformed", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_MissingEventId_ReturnsUnauthorized()
    {
        _signatureVerifier.SetResult(WebhookSignatureResult.Valid());
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { driverId = Guid.NewGuid().ToString() }));

        var result = await _handler.HandleAsync(body, "sig", FixedNow.ToString("o"));

        Assert.True(result.IsUnauthorized);
        Assert.Contains("Missing eventId", result.ErrorMessage);
    }

    // ─── Idempotence (Req 3.7) ───────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_DuplicateEventId_Returns200_NoSideEffects()
    {
        _signatureVerifier.SetResult(WebhookSignatureResult.Valid());
        var driverId = Guid.NewGuid();
        var body = BuildPayload("evt-dup", driverId);

        // First call — should persist and fan out
        _wsConnectionRepository.SetConnections(driverId, new List<WsConnection>());
        var result1 = await _handler.HandleAsync(body, "sig", FixedNow.ToString("o"));
        Assert.True(result1.IsSuccess);
        Assert.Single(_paymentEventRepository.StoredEvents);

        // Second call with same eventId — should be idempotent no-op
        _queuedMessageRepository.EnqueuedMessages.Clear();
        _webSocketPushService.PushedMessages.Clear();

        var result2 = await _handler.HandleAsync(body, "sig", FixedNow.ToString("o"));

        Assert.True(result2.IsSuccess);
        Assert.Single(_paymentEventRepository.StoredEvents); // Still only one event
        Assert.Empty(_queuedMessageRepository.EnqueuedMessages); // No new enqueue
        Assert.Empty(_webSocketPushService.PushedMessages); // No new push
    }

    // ─── Online Driver Push (Req 3.2) ────────────────────────────────────

    [Fact]
    public async Task HandleAsync_DriverOnline_PushesPaymentConfirmedEnvelope()
    {
        _signatureVerifier.SetResult(WebhookSignatureResult.Valid());
        var driverId = Guid.NewGuid();
        var connectionId = "conn-abc-123";
        var body = BuildPayload("evt-online", driverId);

        _wsConnectionRepository.SetConnections(driverId, new List<WsConnection>
        {
            CreateConnection(driverId, connectionId)
        });
        _webSocketPushService.SetPushResult(true);

        var result = await _handler.HandleAsync(body, "sig", FixedNow.ToString("o"));

        Assert.True(result.IsSuccess);
        Assert.Single(_webSocketPushService.PushedMessages);
        Assert.Equal(connectionId, _webSocketPushService.PushedMessages[0].ConnectionId);

        // Verify the envelope contains payment.confirmed action
        var envelope = JsonSerializer.Deserialize<JsonElement>(_webSocketPushService.PushedMessages[0].Payload);
        Assert.Equal("payment.confirmed", envelope.GetProperty("action").GetString());
        Assert.Equal("evt-online", envelope.GetProperty("data").GetProperty("eventId").GetString());
        Assert.Equal(driverId.ToString(), envelope.GetProperty("data").GetProperty("driverId").GetString());

        // Should NOT enqueue when push succeeds
        Assert.Empty(_queuedMessageRepository.EnqueuedMessages);
    }

    [Fact]
    public async Task HandleAsync_DriverOnline_MultipleConnections_PushesToAll()
    {
        _signatureVerifier.SetResult(WebhookSignatureResult.Valid());
        var driverId = Guid.NewGuid();
        var body = BuildPayload("evt-multi", driverId);

        _wsConnectionRepository.SetConnections(driverId, new List<WsConnection>
        {
            CreateConnection(driverId, "conn-1"),
            CreateConnection(driverId, "conn-2")
        });
        _webSocketPushService.SetPushResult(true);

        var result = await _handler.HandleAsync(body, "sig", FixedNow.ToString("o"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _webSocketPushService.PushedMessages.Count);
        Assert.Empty(_queuedMessageRepository.EnqueuedMessages);
    }

    // ─── Offline Driver Enqueue (Req 3.6) ────────────────────────────────

    [Fact]
    public async Task HandleAsync_DriverOffline_EnqueuesInQueuedMessages()
    {
        _signatureVerifier.SetResult(WebhookSignatureResult.Valid());
        var driverId = Guid.NewGuid();
        var body = BuildPayload("evt-offline", driverId);

        // No connections — driver is offline
        _wsConnectionRepository.SetConnections(driverId, new List<WsConnection>());

        var result = await _handler.HandleAsync(body, "sig", FixedNow.ToString("o"));

        Assert.True(result.IsSuccess);
        Assert.Single(_queuedMessageRepository.EnqueuedMessages);
        Assert.Equal(driverId, _queuedMessageRepository.EnqueuedMessages[0].DriverId);
        Assert.Equal("evt-offline", _queuedMessageRepository.EnqueuedMessages[0].EventId);
        Assert.Empty(_webSocketPushService.PushedMessages);
    }

    [Fact]
    public async Task HandleAsync_PushFails_FallsBackToEnqueue()
    {
        _signatureVerifier.SetResult(WebhookSignatureResult.Valid());
        var driverId = Guid.NewGuid();
        var body = BuildPayload("evt-pushfail", driverId);

        _wsConnectionRepository.SetConnections(driverId, new List<WsConnection>
        {
            CreateConnection(driverId, "conn-stale")
        });
        _webSocketPushService.SetPushResult(false); // Simulate 410 Gone

        var result = await _handler.HandleAsync(body, "sig", FixedNow.ToString("o"));

        Assert.True(result.IsSuccess);
        Assert.Single(_webSocketPushService.PushedMessages); // Attempted push
        Assert.Single(_queuedMessageRepository.EnqueuedMessages); // Fell back to enqueue
    }

    [Fact]
    public async Task HandleAsync_SomePushesSucceed_DoesNotEnqueue()
    {
        _signatureVerifier.SetResult(WebhookSignatureResult.Valid());
        var driverId = Guid.NewGuid();
        var body = BuildPayload("evt-partial", driverId);

        _wsConnectionRepository.SetConnections(driverId, new List<WsConnection>
        {
            CreateConnection(driverId, "conn-good"),
            CreateConnection(driverId, "conn-stale")
        });
        // First push succeeds, second fails
        _webSocketPushService.SetPushResults(new[] { true, false });

        var result = await _handler.HandleAsync(body, "sig", FixedNow.ToString("o"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _webSocketPushService.PushedMessages.Count);
        // At least one push succeeded, so no enqueue
        Assert.Empty(_queuedMessageRepository.EnqueuedMessages);
    }

    // ─── Envelope Structure ──────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_EnvelopeContainsRequiredFields()
    {
        _signatureVerifier.SetResult(WebhookSignatureResult.Valid());
        var driverId = Guid.NewGuid();
        var payerId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var body = BuildPayload("evt-envelope", driverId, payerId: payerId, routeId: routeId,
            payerName: "Juan Dela Cruz", amountCentavos: 1500);

        _wsConnectionRepository.SetConnections(driverId, new List<WsConnection>
        {
            CreateConnection(driverId, "conn-env")
        });
        _webSocketPushService.SetPushResult(true);

        await _handler.HandleAsync(body, "sig", FixedNow.ToString("o"));

        var envelope = JsonSerializer.Deserialize<JsonElement>(_webSocketPushService.PushedMessages[0].Payload);
        var data = envelope.GetProperty("data");

        Assert.Equal("payment.confirmed", envelope.GetProperty("action").GetString());
        Assert.True(envelope.TryGetProperty("requestId", out _));
        Assert.True(envelope.TryGetProperty("emittedAt", out _));
        Assert.Equal("evt-envelope", data.GetProperty("eventId").GetString());
        Assert.Equal(driverId.ToString(), data.GetProperty("driverId").GetString());
        Assert.Equal(payerId.ToString(), data.GetProperty("payerId").GetString());
        Assert.Equal("Juan Dela Cruz", data.GetProperty("payerName").GetString());
        Assert.Equal(routeId.ToString(), data.GetProperty("routeId").GetString());
        Assert.Equal(1500, data.GetProperty("amountCentavos").GetInt32());
        Assert.Equal("PHP", data.GetProperty("currency").GetString());
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static byte[] BuildPayload(
        string eventId,
        Guid driverId,
        Guid? payerId = null,
        Guid? routeId = null,
        string? payerName = null,
        int amountCentavos = 1000)
    {
        var payload = new
        {
            EventId = eventId,
            DriverId = driverId.ToString(),
            PayerId = (payerId ?? Guid.NewGuid()).ToString(),
            PayerName = payerName ?? "Test Payer",
            RouteId = (routeId ?? Guid.NewGuid()).ToString(),
            AmountCentavos = amountCentavos,
            Currency = "PHP",
            WalletProvider = "MockWallet",
            WalletTransactionId = $"txn-{eventId}",
            OccurredAt = FixedNow.UtcDateTime
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
    }

    private static WsConnection CreateConnection(Guid userId, string connectionId)
    {
        return new WsConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Role = UserRole.Driver,
            ConnectionId = connectionId,
            ConnectedAt = FixedNow.UtcDateTime.AddMinutes(-10),
            ExpiresAt = FixedNow.UtcDateTime.AddHours(24),
            CreatedAt = FixedNow.UtcDateTime.AddMinutes(-10),
            UpdatedAt = FixedNow.UtcDateTime.AddMinutes(-10)
        };
    }

    // ─── Fakes ───────────────────────────────────────────────────────────

    private sealed class FakeSignatureVerifier : IWebhookSignatureVerifier
    {
        private WebhookSignatureResult _result = WebhookSignatureResult.Valid();

        public void SetResult(WebhookSignatureResult result) => _result = result;

        public Task<WebhookSignatureResult> VerifyAsync(
            byte[] rawBody, string? signature, string? timestamp, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class FakePaymentEventRepository : IPaymentEventRepository
    {
        private readonly HashSet<string> _existingEventIds = new();
        public List<PaymentEvent> StoredEvents { get; } = new();

        public Task<bool> PutEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken = default)
        {
            if (_existingEventIds.Contains(paymentEvent.EventId))
                return Task.FromResult(false); // Duplicate — conditional write fails

            _existingEventIds.Add(paymentEvent.EventId);
            StoredEvents.Add(paymentEvent);
            return Task.FromResult(true);
        }

        public Task<PaymentEvent?> GetEventByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentEvent?>(StoredEvents.FirstOrDefault(e => e.Id == eventId));

        public Task<IReadOnlyList<PaymentEvent>> GetEventsByDriverAsync(Guid driverId, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PaymentEvent>>(StoredEvents.Where(e => e.DriverId == driverId).Take(limit).ToList());
    }

    private sealed class FakeWsConnectionRepository : IWsConnectionRepository
    {
        private readonly Dictionary<Guid, IReadOnlyList<WsConnection>> _connections = new();

        public void SetConnections(Guid userId, IReadOnlyList<WsConnection> connections)
            => _connections[userId] = connections;

        public Task<IReadOnlyList<WsConnection>> GetConnectionsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            _connections.TryGetValue(userId, out var conns);
            return Task.FromResult<IReadOnlyList<WsConnection>>(conns ?? Array.Empty<WsConnection>());
        }

        public Task RegisterConnectionAsync(WsConnection connection, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<WsConnection?> GetConnectionByIdAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.FromResult<WsConnection?>(null);

        public Task UpdateSubscriptionAsync(string connectionId, string? bbox, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WsConnection>> GetSubscribedConnectionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(Array.Empty<WsConnection>());
    }

    private sealed class FakeWebSocketPushService : IWebSocketPushService
    {
        private bool _defaultResult = true;
        private Queue<bool>? _results;

        public List<(string ConnectionId, string Payload)> PushedMessages { get; } = new();

        public void SetPushResult(bool result) => _defaultResult = result;

        public void SetPushResults(IEnumerable<bool> results) => _results = new Queue<bool>(results);

        public Task<bool> PostToConnectionAsync(string connectionId, string payload, CancellationToken cancellationToken = default)
        {
            PushedMessages.Add((connectionId, payload));
            var result = _results?.Count > 0 ? _results.Dequeue() : _defaultResult;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeQueuedMessageRepository : IQueuedMessageRepository
    {
        public List<(Guid DriverId, string EventId, DateTime OccurredAt, string Payload)> EnqueuedMessages { get; } = new();

        public Task EnqueueAsync(Guid driverId, string eventId, DateTime occurredAt, string payload, CancellationToken cancellationToken = default)
        {
            EnqueuedMessages.Add((driverId, eventId, occurredAt, payload));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QueuedMessage>> DrainAsync(Guid driverId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<QueuedMessage>>(Array.Empty<QueuedMessage>());

        public Task<int> CountAsync(Guid driverId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
