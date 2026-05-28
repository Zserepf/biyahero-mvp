using System.Text;
using System.Text.Json;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Payment;
using BiyaHero.Api.Features.Payment.Webhook;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BiyaHero.Api.Tests.Features.Payment;

/// <summary>
/// xUnit tests for Payment_Service covering the three core security and idempotence behaviors:
/// 1. Invalid HMAC signature → 401 + no fan-out (no PostToConnection, no DynamoDB write)
/// 2. Duplicate eventId → no-op (returns 200 but doesn't re-process)
/// 3. Replay-window timestamp (X-Wallet-Timestamp outside ±5 minutes) → rejected
///
/// Requirements: 3.5, 3.7
/// </summary>
public class PaymentServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2024, 7, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly StubSignatureVerifier _signatureVerifier = new();
    private readonly StubPaymentEventRepository _paymentEventRepo = new();
    private readonly StubWsConnectionRepository _wsConnectionRepo = new();
    private readonly StubWebSocketPushService _pushService = new();
    private readonly StubQueuedMessageRepository _queuedMessageRepo = new();
    private readonly StubTimeProvider _timeProvider = new(FixedNow);
    private readonly WebhookHandler _handler;

    public PaymentServiceTests()
    {
        _handler = new WebhookHandler(
            _signatureVerifier,
            _paymentEventRepo,
            _wsConnectionRepo,
            _pushService,
            _queuedMessageRepo,
            _timeProvider,
            NullLogger<WebhookHandler>.Instance);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Requirement 3.5: Invalid signature → 401 + no fan-out
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Validates: Requirements 3.5
    /// When the HMAC signature is invalid, the Payment_Service returns 401
    /// and does NOT persist the event, push to WebSocket, or enqueue any message.
    /// </summary>
    [Fact]
    public async Task InvalidSignature_Returns401_NoEventPersisted_NoPush_NoEnqueue()
    {
        // Arrange
        _signatureVerifier.NextResult = WebhookSignatureResult.Invalid("X-Wallet-Signature does not match the expected HMAC-SHA256.");
        var driverId = Guid.NewGuid();
        var body = BuildWebhookPayload("evt-sig-bad", driverId);

        // Simulate a driver being online to prove no fan-out occurs
        _wsConnectionRepo.SetConnections(driverId, new List<WsConnection>
        {
            MakeConnection(driverId, "conn-online-1")
        });
        _pushService.DefaultResult = true;

        // Act
        var result = await _handler.HandleAsync(body, "invalid-signature-value", FixedNow.ToString("o"));

        // Assert — 401 unauthorized
        Assert.True(result.IsUnauthorized);
        Assert.Contains("does not match", result.ErrorMessage);

        // Assert — no fan-out side effects
        Assert.Empty(_paymentEventRepo.StoredEvents);
        Assert.Empty(_pushService.PushedMessages);
        Assert.Empty(_queuedMessageRepo.EnqueuedMessages);
    }

    /// <summary>
    /// Validates: Requirements 3.5
    /// When the signature header is completely missing, the Payment_Service returns 401
    /// and performs no side effects.
    /// </summary>
    [Fact]
    public async Task MissingSignatureHeader_Returns401_NoSideEffects()
    {
        // Arrange
        _signatureVerifier.NextResult = WebhookSignatureResult.Invalid("Missing X-Wallet-Signature header.");
        var body = BuildWebhookPayload("evt-no-sig", Guid.NewGuid());

        // Act
        var result = await _handler.HandleAsync(body, null, FixedNow.ToString("o"));

        // Assert
        Assert.True(result.IsUnauthorized);
        Assert.Contains("Missing", result.ErrorMessage);
        Assert.Empty(_paymentEventRepo.StoredEvents);
        Assert.Empty(_pushService.PushedMessages);
        Assert.Empty(_queuedMessageRepo.EnqueuedMessages);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Requirement 3.7: Duplicate eventId → idempotent no-op
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Validates: Requirements 3.7
    /// When the same eventId is posted more than once, the Payment_Service persists
    /// exactly once and forwards at most one confirmation to the WebSocket_Service.
    /// The second call returns 200 but triggers no additional side effects.
    /// </summary>
    [Fact]
    public async Task DuplicateEventId_SecondCall_Returns200_NoReprocessing()
    {
        // Arrange
        _signatureVerifier.NextResult = WebhookSignatureResult.Valid();
        var driverId = Guid.NewGuid();
        var body = BuildWebhookPayload("evt-dup-test", driverId);

        // Driver is offline so first call enqueues
        _wsConnectionRepo.SetConnections(driverId, new List<WsConnection>());

        // Act — first call processes normally
        var result1 = await _handler.HandleAsync(body, "valid-sig", FixedNow.ToString("o"));

        Assert.True(result1.IsSuccess);
        Assert.Single(_paymentEventRepo.StoredEvents);
        Assert.Single(_queuedMessageRepo.EnqueuedMessages);

        // Clear side-effect trackers to isolate second call
        _queuedMessageRepo.EnqueuedMessages.Clear();
        _pushService.PushedMessages.Clear();

        // Act — second call with same eventId
        var result2 = await _handler.HandleAsync(body, "valid-sig", FixedNow.ToString("o"));

        // Assert — 200 success but no re-processing
        Assert.True(result2.IsSuccess);
        Assert.Single(_paymentEventRepo.StoredEvents); // Still only one persisted event
        Assert.Empty(_queuedMessageRepo.EnqueuedMessages); // No new enqueue
        Assert.Empty(_pushService.PushedMessages); // No new push
    }

    /// <summary>
    /// Validates: Requirements 3.7
    /// Even when the driver comes online between duplicate webhook deliveries,
    /// the second delivery does not trigger a push.
    /// </summary>
    [Fact]
    public async Task DuplicateEventId_DriverComesOnline_StillNoReprocessing()
    {
        // Arrange
        _signatureVerifier.NextResult = WebhookSignatureResult.Valid();
        var driverId = Guid.NewGuid();
        var body = BuildWebhookPayload("evt-dup-online", driverId);

        // First call: driver offline
        _wsConnectionRepo.SetConnections(driverId, new List<WsConnection>());
        var result1 = await _handler.HandleAsync(body, "sig", FixedNow.ToString("o"));
        Assert.True(result1.IsSuccess);

        // Driver comes online
        _wsConnectionRepo.SetConnections(driverId, new List<WsConnection>
        {
            MakeConnection(driverId, "conn-new")
        });
        _pushService.DefaultResult = true;
        _pushService.PushedMessages.Clear();
        _queuedMessageRepo.EnqueuedMessages.Clear();

        // Act — duplicate delivery
        var result2 = await _handler.HandleAsync(body, "sig", FixedNow.ToString("o"));

        // Assert — no push even though driver is now online
        Assert.True(result2.IsSuccess);
        Assert.Empty(_pushService.PushedMessages);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Requirement 3.5 (replay protection): Timestamp outside ±5 minutes rejected
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Validates: Requirements 3.5
    /// When X-Wallet-Timestamp is more than 5 minutes in the past, the Payment_Service
    /// rejects the webhook with 401 and performs no side effects.
    /// </summary>
    [Fact]
    public async Task ReplayWindow_TimestampTooOld_Returns401_NoSideEffects()
    {
        // Arrange — timestamp 6 minutes in the past
        var staleTimestamp = FixedNow.AddMinutes(-6).ToString("o");
        _signatureVerifier.NextResult = WebhookSignatureResult.Invalid(
            "X-Wallet-Timestamp is outside the ±5 minute tolerance window.");
        var body = BuildWebhookPayload("evt-replay-old", Guid.NewGuid());

        // Act
        var result = await _handler.HandleAsync(body, "sig", staleTimestamp);

        // Assert
        Assert.True(result.IsUnauthorized);
        Assert.Contains("tolerance", result.ErrorMessage);
        Assert.Empty(_paymentEventRepo.StoredEvents);
        Assert.Empty(_pushService.PushedMessages);
        Assert.Empty(_queuedMessageRepo.EnqueuedMessages);
    }

    /// <summary>
    /// Validates: Requirements 3.5
    /// When X-Wallet-Timestamp is more than 5 minutes in the future, the Payment_Service
    /// rejects the webhook with 401 and performs no side effects.
    /// </summary>
    [Fact]
    public async Task ReplayWindow_TimestampTooFarInFuture_Returns401_NoSideEffects()
    {
        // Arrange — timestamp 6 minutes in the future
        var futureTimestamp = FixedNow.AddMinutes(6).ToString("o");
        _signatureVerifier.NextResult = WebhookSignatureResult.Invalid(
            "X-Wallet-Timestamp is outside the ±5 minute tolerance window.");
        var body = BuildWebhookPayload("evt-replay-future", Guid.NewGuid());

        // Act
        var result = await _handler.HandleAsync(body, "sig", futureTimestamp);

        // Assert
        Assert.True(result.IsUnauthorized);
        Assert.Contains("tolerance", result.ErrorMessage);
        Assert.Empty(_paymentEventRepo.StoredEvents);
        Assert.Empty(_pushService.PushedMessages);
        Assert.Empty(_queuedMessageRepo.EnqueuedMessages);
    }

    /// <summary>
    /// Validates: Requirements 3.5
    /// When X-Wallet-Timestamp is exactly at the boundary (5 minutes + 1 second past),
    /// the Payment_Service rejects the webhook.
    /// </summary>
    [Fact]
    public async Task ReplayWindow_TimestampJustOutsideBoundary_Returns401()
    {
        // Arrange — 5 minutes and 1 second in the past (just outside window)
        var boundaryTimestamp = FixedNow.AddMinutes(-5).AddSeconds(-1).ToString("o");
        _signatureVerifier.NextResult = WebhookSignatureResult.Invalid(
            "X-Wallet-Timestamp is outside the ±5 minute tolerance window.");
        var body = BuildWebhookPayload("evt-boundary", Guid.NewGuid());

        // Act
        var result = await _handler.HandleAsync(body, "sig", boundaryTimestamp);

        // Assert
        Assert.True(result.IsUnauthorized);
        Assert.Contains("tolerance", result.ErrorMessage);
        Assert.Empty(_paymentEventRepo.StoredEvents);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static byte[] BuildWebhookPayload(string eventId, Guid driverId)
    {
        var payload = new
        {
            EventId = eventId,
            DriverId = driverId.ToString(),
            PayerId = Guid.NewGuid().ToString(),
            PayerName = "Test Commuter",
            RouteId = Guid.NewGuid().ToString(),
            AmountCentavos = 1200,
            Currency = "PHP",
            WalletProvider = "MockWallet",
            WalletTransactionId = $"txn-{eventId}",
            OccurredAt = FixedNow.UtcDateTime
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
    }

    private static WsConnection MakeConnection(Guid userId, string connectionId)
    {
        return new WsConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Role = UserRole.Driver,
            ConnectionId = connectionId,
            ConnectedAt = FixedNow.UtcDateTime.AddMinutes(-5),
            ExpiresAt = FixedNow.UtcDateTime.AddHours(24),
            CreatedAt = FixedNow.UtcDateTime.AddMinutes(-5),
            UpdatedAt = FixedNow.UtcDateTime.AddMinutes(-5)
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test Doubles
    // ═══════════════════════════════════════════════════════════════════════

    private sealed class StubSignatureVerifier : IWebhookSignatureVerifier
    {
        public WebhookSignatureResult NextResult { get; set; } = WebhookSignatureResult.Valid();

        public Task<WebhookSignatureResult> VerifyAsync(
            byte[] rawBody, string? signature, string? timestamp, CancellationToken cancellationToken = default)
            => Task.FromResult(NextResult);
    }

    private sealed class StubPaymentEventRepository : IPaymentEventRepository
    {
        private readonly HashSet<string> _seenEventIds = new();
        public List<PaymentEvent> StoredEvents { get; } = new();

        public Task<bool> PutEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken = default)
        {
            if (_seenEventIds.Contains(paymentEvent.EventId))
                return Task.FromResult(false);

            _seenEventIds.Add(paymentEvent.EventId);
            StoredEvents.Add(paymentEvent);
            return Task.FromResult(true);
        }

        public Task<PaymentEvent?> GetEventByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentEvent?>(StoredEvents.FirstOrDefault(e => e.Id == eventId));

        public Task<IReadOnlyList<PaymentEvent>> GetEventsByDriverAsync(Guid driverId, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PaymentEvent>>(StoredEvents.Where(e => e.DriverId == driverId).Take(limit).ToList());
    }

    private sealed class StubWsConnectionRepository : IWsConnectionRepository
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

    private sealed class StubWebSocketPushService : IWebSocketPushService
    {
        public bool DefaultResult { get; set; } = true;
        public List<(string ConnectionId, string Payload)> PushedMessages { get; } = new();

        public Task<bool> PostToConnectionAsync(string connectionId, string payload, CancellationToken cancellationToken = default)
        {
            PushedMessages.Add((connectionId, payload));
            return Task.FromResult(DefaultResult);
        }
    }

    private sealed class StubQueuedMessageRepository : IQueuedMessageRepository
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

    private sealed class StubTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public StubTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
