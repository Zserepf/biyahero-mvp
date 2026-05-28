using System.Collections.Concurrent;
using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Features.Payment.Webhook;

/// <summary>
/// Thread-safe in-memory store for PaymentEvents.
/// Uses ConcurrentDictionary for safe concurrent access.
/// Replaceable with DynamoDB conditional-write repository post-MVP.
/// </summary>
public sealed class InMemoryPaymentEventStore : IPaymentEventStore
{
    private readonly ConcurrentDictionary<string, PaymentEvent> _events = new();

    public bool Exists(string eventId) => _events.ContainsKey(eventId);

    public void Add(string eventId, PaymentEvent paymentEvent) =>
        _events.TryAdd(eventId, paymentEvent);

    public PaymentEvent? Get(string eventId) =>
        _events.TryGetValue(eventId, out var evt) ? evt : null;
}
