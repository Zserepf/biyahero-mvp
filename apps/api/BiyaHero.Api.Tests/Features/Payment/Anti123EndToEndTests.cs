using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Payment;
using BiyaHero.Api.Features.Payment.Webhook;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;
using BiyaHero.Api.WebSockets;
using Microsoft.Extensions.Logging.Abstractions;

namespace BiyaHero.Api.Tests.Features.Payment;

/// <summary>
/// Integration tests for the Anti-123 end-to-end flow.
/// Verifies the full pipeline:
///   Webhook → DynamoDB persist → PostToConnection (driver online)
///   Webhook → DynamoDB persist → QueuedMessages → $connect drain (driver offline)
///
/// Uses coordinated fakes that share state to simulate the real data flow
/// between WebhookHandler and ConnectHandler without external infrastructure.
///
/// Validates: Requirements 3.1, 3.2, 3.6
/// </summary>
public class Anti123EndToEndTests
{
    private static readonly DateTimeOffset FixedNow = new(2024, 7, 1, 10, 0, 0, TimeSpan.Zero);

    // Shared state fakes — both handlers interact with the same repositories
    private readonly SharedPaymentEventRepository _paymentEventRepository = new();
    private readonly SharedWsConnectionRepository _wsConnectionRepository = new();
    private readonly SharedQueuedMessageRepository _queuedMessageRepository = new();
    private readonly SharedWebSocketPushService _webSocketPushService = new();
    private readonly AlwaysValidSignatureVerifier _signatureVerifier = new();
    private readonly FakeJwtService _jwtService = new();
    private readonly FakeTimeProvider _timeProvider;

    private readonly WebhookHandler _webhookHandler;
    private readonly ConnectHandler _connectHandler;

    public Anti123EndToEndTests()
    {
        _timeProvider = new FakeTimeProvider(FixedNow);

        _webhookHandler = new WebhookHandler(
            _signatureVerifier,
            _paymentEventRepository,
            _wsConnectionRepository,
            _webSocketPushService,
            _queuedMessageRepository,
            _timeProvider,
            NullLogger<WebhookHandler>.Instance);

        _connectHandler = new ConnectHandler(
            _jwtService,
            _wsConnectionRepository,
            _queuedMessageRepository,
            _webSocketPushService,
            _timeProvider,
            NullLogger<ConnectHandler>.Instance);
    }

    // ─── Scenario 1: Driver Online — Webhook → Persist → Push ─────────────

    /// <summary>
    /// End-to-end: Webhook arrives → PaymentEvent persisted → payment.confirmed
    /// pushed to connected driver via PostToConnection.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public async Task DriverOnline_WebhookPersistsAndPushesPaymentConfirmed()
    {
        // Arrange — driver is connected
        var driverId = Guid.NewGuid();
        var connectionId = "conn-driver-online";
        RegisterDriverConnection(driverId, connectionId);

        var eventId = "evt-e2e-online-001";
        var payerId = Guid.NewGuid();
        var body = BuildWebhookPayload(eventId, driverId, payerId, "Maria Santos", 2500);

        // Act — webhook arrives
        var result = await _webhookHandler.HandleAsync(
            body, "valid-sig", FixedNow.ToString("o"));

        // Assert — webhook returns success
        Assert.True(result.IsSuccess);

        // Assert — PaymentEvent persisted in DynamoDB
        Assert.Single(_paymentEventRepository.StoredEvents);
        var storedEvent = _paymentEventRepository.StoredEvents[0];
        Assert.Equal(eventId, storedEvent.EventId);
        Assert.Equal(driverId, storedEvent.DriverId);
        Assert.Equal(payerId, storedEvent.PayerId);
        Assert.Equal("Maria Santos", storedEvent.PayerName);
        Assert.Equal(2500, storedEvent.AmountCentavos);
        Assert.Equal(PaymentStatus.Confirmed, storedEvent.Status);

        // Assert — payment.confirmed pushed to driver's connection
        Assert.Single(_webSocketPushService.PushedMessages);
        var pushed = _webSocketPushService.PushedMessages[0];
        Assert.Equal(connectionId, pushed.ConnectionId);

        // Assert — envelope structure is correct
        var envelope = JsonSerializer.Deserialize<JsonElement>(pushed.Payload);
        Assert.Equal("payment.confirmed", envelope.GetProperty("action").GetString());
        Assert.Equal(eventId, envelope.GetProperty("data").GetProperty("eventId").GetString());
        Assert.Equal(driverId.ToString(), envelope.GetProperty("data").GetProperty("driverId").GetString());
        Assert.Equal(payerId.ToString(), envelope.GetProperty("data").GetProperty("payerId").GetString());
        Assert.Equal("Maria Santos", envelope.GetProperty("data").GetProperty("payerName").GetString());
        Assert.Equal(2500, envelope.GetProperty("data").GetProperty("amountCentavos").GetInt32());

        // Assert — nothing enqueued (driver was online)
        Assert.Empty(_queuedMessageRepository.Messages);
    }

    /// <summary>
    /// End-to-end: Multiple webhooks for the same driver who is online —
    /// each payment is persisted and pushed independently.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public async Task DriverOnline_MultiplePayments_AllPersistedAndPushed()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var connectionId = "conn-multi-pay";
        RegisterDriverConnection(driverId, connectionId);

        var body1 = BuildWebhookPayload("evt-multi-1", driverId, Guid.NewGuid(), "Payer A", 1000);
        var body2 = BuildWebhookPayload("evt-multi-2", driverId, Guid.NewGuid(), "Payer B", 1500);

        // Act
        var result1 = await _webhookHandler.HandleAsync(body1, "sig", FixedNow.ToString("o"));
        var result2 = await _webhookHandler.HandleAsync(body2, "sig", FixedNow.ToString("o"));

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(2, _paymentEventRepository.StoredEvents.Count);
        Assert.Equal(2, _webSocketPushService.PushedMessages.Count);
        Assert.All(_webSocketPushService.PushedMessages, m => Assert.Equal(connectionId, m.ConnectionId));
    }

    // ─── Scenario 2: Driver Offline — Webhook → Persist → Queue → Connect → Drain ──

    /// <summary>
    /// End-to-end: Driver is offline when webhook arrives → event persisted →
    /// message queued → driver reconnects → queued messages drained and delivered.
    /// Validates: Requirements 3.1, 3.2, 3.6
    /// </summary>
    [Fact]
    public async Task DriverOffline_WebhookQueues_ThenConnectDrains()
    {
        // Arrange — driver is NOT connected (no WsConnection registered)
        var driverId = Guid.NewGuid();
        var eventId = "evt-e2e-offline-001";
        var payerId = Guid.NewGuid();
        var body = BuildWebhookPayload(eventId, driverId, payerId, "Juan Dela Cruz", 3000);

        // Act 1 — webhook arrives while driver is offline
        var webhookResult = await _webhookHandler.HandleAsync(
            body, "valid-sig", FixedNow.ToString("o"));

        // Assert 1 — webhook succeeds, event persisted, message queued
        Assert.True(webhookResult.IsSuccess);
        Assert.Single(_paymentEventRepository.StoredEvents);
        Assert.Equal(eventId, _paymentEventRepository.StoredEvents[0].EventId);
        Assert.Empty(_webSocketPushService.PushedMessages); // No push — driver offline
        Assert.Single(_queuedMessageRepository.Messages); // Message queued

        var queuedMsg = _queuedMessageRepository.Messages[0];
        Assert.Equal(driverId, queuedMsg.DriverId);
        Assert.Equal(eventId, queuedMsg.EventId);

        // Act 2 — driver reconnects via $connect
        var newConnectionId = "conn-reconnect-001";
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(driverId, "driver@test.com", "Driver"));

        var connectResult = await _connectHandler.HandleAsync(
            newConnectionId, token: "valid-jwt");

        // Assert 2 — connection accepted
        Assert.True(connectResult.IsAccepted);
        Assert.Equal(driverId, connectResult.UserId);

        // Assert 2 — queued message drained and pushed to new connection
        Assert.Single(_webSocketPushService.PushedMessages);
        var delivered = _webSocketPushService.PushedMessages[0];
        Assert.Equal(newConnectionId, delivered.ConnectionId);

        // Assert 2 — delivered payload contains the original payment.confirmed envelope
        var envelope = JsonSerializer.Deserialize<JsonElement>(delivered.Payload);
        Assert.Equal("payment.confirmed", envelope.GetProperty("action").GetString());
        Assert.Equal(eventId, envelope.GetProperty("data").GetProperty("eventId").GetString());
        Assert.Equal("Juan Dela Cruz", envelope.GetProperty("data").GetProperty("payerName").GetString());
        Assert.Equal(3000, envelope.GetProperty("data").GetProperty("amountCentavos").GetInt32());

        // Assert 2 — queue is now empty after drain
        Assert.Empty(_queuedMessageRepository.Messages);
    }

    /// <summary>
    /// End-to-end: Multiple payments arrive while driver is offline →
    /// all queued → on reconnect, all drained in chronological order.
    /// Validates: Requirements 3.1, 3.6
    /// </summary>
    [Fact]
    public async Task DriverOffline_MultiplePayments_AllDrainedInOrderOnReconnect()
    {
        // Arrange — driver offline
        var driverId = Guid.NewGuid();

        // Simulate payments arriving at different times
        var body1 = BuildWebhookPayload("evt-q1", driverId, Guid.NewGuid(), "Payer 1", 500);
        var body2 = BuildWebhookPayload("evt-q2", driverId, Guid.NewGuid(), "Payer 2", 750);
        var body3 = BuildWebhookPayload("evt-q3", driverId, Guid.NewGuid(), "Payer 3", 1200);

        // Act 1 — three webhooks arrive while driver is offline
        await _webhookHandler.HandleAsync(body1, "sig", FixedNow.ToString("o"));
        await _webhookHandler.HandleAsync(body2, "sig", FixedNow.AddSeconds(30).ToString("o"));
        await _webhookHandler.HandleAsync(body3, "sig", FixedNow.AddMinutes(1).ToString("o"));

        // Assert 1 — all persisted, all queued, nothing pushed
        Assert.Equal(3, _paymentEventRepository.StoredEvents.Count);
        Assert.Equal(3, _queuedMessageRepository.Messages.Count);
        Assert.Empty(_webSocketPushService.PushedMessages);

        // Act 2 — driver reconnects
        var connectionId = "conn-multi-drain";
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(driverId, "driver@test.com", "Driver"));

        await _connectHandler.HandleAsync(connectionId, token: "valid-jwt");

        // Assert 2 — all three messages drained and pushed
        Assert.Equal(3, _webSocketPushService.PushedMessages.Count);
        Assert.All(_webSocketPushService.PushedMessages, m => Assert.Equal(connectionId, m.ConnectionId));

        // Assert 2 — messages delivered in chronological order
        var payloads = _webSocketPushService.PushedMessages
            .Select(m => JsonSerializer.Deserialize<JsonElement>(m.Payload))
            .ToList();
        Assert.Equal("evt-q1", payloads[0].GetProperty("data").GetProperty("eventId").GetString());
        Assert.Equal("evt-q2", payloads[1].GetProperty("data").GetProperty("eventId").GetString());
        Assert.Equal("evt-q3", payloads[2].GetProperty("data").GetProperty("eventId").GetString());

        // Assert 2 — queue is empty
        Assert.Empty(_queuedMessageRepository.Messages);
    }

    /// <summary>
    /// End-to-end: Driver connects with no queued messages — no push occurs,
    /// connection is still accepted normally.
    /// Validates: Requirement 3.6 (no-op drain)
    /// </summary>
    [Fact]
    public async Task DriverConnects_NoQueuedMessages_NoPushOccurs()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(driverId, "driver@test.com", "Driver"));

        // Act
        var result = await _connectHandler.HandleAsync("conn-empty-queue", token: "valid-jwt");

        // Assert
        Assert.True(result.IsAccepted);
        Assert.Empty(_webSocketPushService.PushedMessages);
    }

    /// <summary>
    /// End-to-end: Payment arrives for driver who is offline, then driver connects,
    /// then another payment arrives while driver is now online.
    /// First payment delivered via drain, second via direct push.
    /// Validates: Requirements 3.1, 3.2, 3.6
    /// </summary>
    [Fact]
    public async Task MixedScenario_OfflinePaymentDrained_ThenOnlinePaymentPushed()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var connectionId = "conn-mixed-scenario";

        // Phase 1: Payment while offline
        var offlineBody = BuildWebhookPayload("evt-offline", driverId, Guid.NewGuid(), "Offline Payer", 1000);
        await _webhookHandler.HandleAsync(offlineBody, "sig", FixedNow.ToString("o"));

        Assert.Single(_queuedMessageRepository.Messages);
        Assert.Empty(_webSocketPushService.PushedMessages);

        // Phase 2: Driver connects — queued message drained
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(driverId, "driver@test.com", "Driver"));
        await _connectHandler.HandleAsync(connectionId, token: "valid-jwt");

        Assert.Single(_webSocketPushService.PushedMessages); // Drained message
        Assert.Empty(_queuedMessageRepository.Messages); // Queue empty

        // Phase 3: Another payment while driver is now online
        // Register the connection so WebhookHandler can find it
        _wsConnectionRepository.RegisterDriverDirectly(driverId, connectionId);

        var onlineBody = BuildWebhookPayload("evt-online", driverId, Guid.NewGuid(), "Online Payer", 2000);
        await _webhookHandler.HandleAsync(onlineBody, "sig", FixedNow.AddMinutes(5).ToString("o"));

        // Assert — second payment pushed directly (not queued)
        Assert.Equal(2, _webSocketPushService.PushedMessages.Count);
        Assert.Equal(connectionId, _webSocketPushService.PushedMessages[1].ConnectionId);
        Assert.Empty(_queuedMessageRepository.Messages); // Still empty — no new queue

        // Verify the second push is the online payment
        var secondEnvelope = JsonSerializer.Deserialize<JsonElement>(
            _webSocketPushService.PushedMessages[1].Payload);
        Assert.Equal("evt-online", secondEnvelope.GetProperty("data").GetProperty("eventId").GetString());
        Assert.Equal("Online Payer", secondEnvelope.GetProperty("data").GetProperty("payerName").GetString());
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private void RegisterDriverConnection(Guid driverId, string connectionId)
    {
        _wsConnectionRepository.RegisterDriverDirectly(driverId, connectionId);
    }

    private static byte[] BuildWebhookPayload(
        string eventId,
        Guid driverId,
        Guid payerId,
        string payerName,
        int amountCentavos)
    {
        var payload = new
        {
            EventId = eventId,
            DriverId = driverId.ToString(),
            PayerId = payerId.ToString(),
            PayerName = payerName,
            RouteId = Guid.NewGuid().ToString(),
            AmountCentavos = amountCentavos,
            Currency = "PHP",
            WalletProvider = "MockWallet",
            WalletTransactionId = $"txn-{eventId}",
            OccurredAt = FixedNow.UtcDateTime
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
    }

    // ─── Shared Fakes (coordinated state between handlers) ───────────────

    /// <summary>
    /// Always-valid signature verifier for integration tests.
    /// Signature verification is tested separately in WebhookSignatureVerifierTests.
    /// </summary>
    private sealed class AlwaysValidSignatureVerifier : IWebhookSignatureVerifier
    {
        public Task<WebhookSignatureResult> VerifyAsync(
            byte[] rawBody, string? signature, string? timestamp,
            CancellationToken cancellationToken = default)
            => Task.FromResult(WebhookSignatureResult.Valid());
    }

    /// <summary>
    /// Shared payment event repository that persists events in memory
    /// with idempotent conditional writes (simulates DynamoDB attribute_not_exists).
    /// </summary>
    private sealed class SharedPaymentEventRepository : IPaymentEventRepository
    {
        private readonly HashSet<string> _existingEventIds = new();
        public List<PaymentEvent> StoredEvents { get; } = new();

        public Task<bool> PutEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken = default)
        {
            if (_existingEventIds.Contains(paymentEvent.EventId))
                return Task.FromResult(false);

            _existingEventIds.Add(paymentEvent.EventId);
            StoredEvents.Add(paymentEvent);
            return Task.FromResult(true);
        }

        public Task<PaymentEvent?> GetEventByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentEvent?>(StoredEvents.FirstOrDefault(e => e.Id == eventId));

        public Task<IReadOnlyList<PaymentEvent>> GetEventsByDriverAsync(Guid driverId, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PaymentEvent>>(
                StoredEvents.Where(e => e.DriverId == driverId).Take(limit).ToList());
    }

    /// <summary>
    /// Shared WsConnection repository that both WebhookHandler and ConnectHandler use.
    /// WebhookHandler queries connections to determine if driver is online.
    /// ConnectHandler registers new connections on $connect.
    /// </summary>
    private sealed class SharedWsConnectionRepository : IWsConnectionRepository
    {
        private readonly List<WsConnection> _connections = new();

        public void RegisterDriverDirectly(Guid driverId, string connectionId)
        {
            _connections.Add(new WsConnection
            {
                Id = Guid.NewGuid(),
                UserId = driverId,
                Role = UserRole.Driver,
                ConnectionId = connectionId,
                ConnectedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public Task RegisterConnectionAsync(WsConnection connection, CancellationToken cancellationToken = default)
        {
            _connections.Add(connection);
            return Task.CompletedTask;
        }

        public Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
        {
            _connections.RemoveAll(c => c.ConnectionId == connectionId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WsConnection>> GetConnectionsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var result = _connections.Where(c => c.UserId == userId).ToList();
            return Task.FromResult<IReadOnlyList<WsConnection>>(result);
        }

        public Task<WsConnection?> GetConnectionByIdAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.FromResult<WsConnection?>(_connections.FirstOrDefault(c => c.ConnectionId == connectionId));

        public Task UpdateSubscriptionAsync(string connectionId, string? bbox, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WsConnection>> GetSubscribedConnectionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WsConnection>>(Array.Empty<WsConnection>());
    }

    /// <summary>
    /// Shared queued message repository that both WebhookHandler (enqueue)
    /// and ConnectHandler (drain) interact with. Messages are stored in memory
    /// and removed on drain, simulating the DynamoDB batch-delete behavior.
    /// </summary>
    private sealed class SharedQueuedMessageRepository : IQueuedMessageRepository
    {
        public List<QueuedMessage> Messages { get; } = new();

        public Task EnqueueAsync(Guid driverId, string eventId, DateTime occurredAt, string payload, CancellationToken cancellationToken = default)
        {
            Messages.Add(new QueuedMessage(
                id: Guid.NewGuid(),
                createdAt: DateTime.UtcNow,
                updatedAt: DateTime.UtcNow,
                driverId: driverId,
                eventId: eventId,
                occurredAt: occurredAt,
                payload: payload,
                expiresAt: DateTime.UtcNow.AddHours(24)));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QueuedMessage>> DrainAsync(Guid driverId, CancellationToken cancellationToken = default)
        {
            var driverMessages = Messages
                .Where(m => m.DriverId == driverId)
                .OrderBy(m => m.OccurredAt)
                .ToList();

            Messages.RemoveAll(m => m.DriverId == driverId);
            return Task.FromResult<IReadOnlyList<QueuedMessage>>(driverMessages);
        }

        public Task<int> CountAsync(Guid driverId, CancellationToken cancellationToken = default)
            => Task.FromResult(Messages.Count(m => m.DriverId == driverId));
    }

    /// <summary>
    /// Shared WebSocket push service that records all pushed messages.
    /// Used by both WebhookHandler (direct push) and ConnectHandler (drain push).
    /// </summary>
    private sealed class SharedWebSocketPushService : IWebSocketPushService
    {
        public List<(string ConnectionId, string Payload)> PushedMessages { get; } = new();

        public Task<bool> PostToConnectionAsync(string connectionId, string payload, CancellationToken cancellationToken = default)
        {
            PushedMessages.Add((connectionId, payload));
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Fake JWT service for the ConnectHandler.
    /// </summary>
    private sealed class FakeJwtService : IJwtService
    {
        private JwtValidationResult _validationResult = JwtValidationResult.Failure("Not configured.");

        public void SetValidationResult(JwtValidationResult result) => _validationResult = result;

        public Task<string> GenerateAccessTokenAsync(User user, CancellationToken cancellationToken = default)
            => Task.FromResult("fake-access-token");

        public Task<string> GenerateRefreshTokenAsync(User user, CancellationToken cancellationToken = default)
            => Task.FromResult("fake-refresh-token");

        public Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (_validationResult.IsValid)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, _validationResult.UserId!.Value.ToString()),
                    new(ClaimTypes.Email, _validationResult.Email ?? ""),
                    new(ClaimTypes.Role, _validationResult.Role ?? "Commuter")
                };
                var identity = new ClaimsIdentity(claims, "Bearer");
                return Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(identity));
            }
            return Task.FromResult<ClaimsPrincipal?>(null);
        }

        public Task<Guid?> GetUserIdFromTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(_validationResult.UserId);

        public Task<JwtValidationResult> ValidateTokenDetailedAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(_validationResult);
    }

    /// <summary>
    /// Fake TimeProvider that returns a fixed time for deterministic tests.
    /// </summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
