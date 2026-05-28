using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Tests.Properties;

/// <summary>
/// Property-based tests for PaymentEvent serialization round-trip.
/// Validates: Requirements 3.10, 3.11
/// Feature: biyahero-mvp, Property 2: Round-trip PaymentEvent serialization
/// </summary>
public class PaymentEventRoundTripPropertyTests
{
    private static readonly string[] WalletProviders = { "GCash", "Maya", "MockWallet", "PayMongo", "Coins.ph" };
    private static readonly string[] Currencies = { "PHP", "USD", "EUR" };

    /// <summary>
    /// Generator for a UTC DateTime within a reasonable range (2020–2025).
    /// Uses DateTimeKind.Utc to ensure round-trip formatting with "o" specifier.
    /// </summary>
    private static Gen<DateTime> UtcDateTimeGen =>
        from year in Gen.Choose(2020, 2025)
        from month in Gen.Choose(1, 12)
        from day in Gen.Choose(1, 28)
        from hour in Gen.Choose(0, 23)
        from minute in Gen.Choose(0, 59)
        from second in Gen.Choose(0, 59)
        select new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

    /// <summary>
    /// Generator for non-empty alphanumeric strings suitable for event IDs and names.
    /// </summary>
    private static Gen<string> NonEmptyAlphanumericGen =>
        from length in Gen.Choose(5, 20)
        from chars in Gen.ListOf(length, Gen.Elements(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray()))
        select new string(chars.ToArray());

    /// <summary>
    /// Generator for a valid hex string (simulating a SHA-256 hash).
    /// </summary>
    private static Gen<string> HexHashGen =>
        from chars in Gen.ListOf(64, Gen.Elements("0123456789abcdef".ToCharArray()))
        select new string(chars.ToArray());

    /// <summary>
    /// Custom Arbitrary for generating valid PaymentEvent instances with random data.
    /// Constrains values to realistic ranges while covering all PaymentStatus variants.
    /// </summary>
    private static Arbitrary<PaymentEvent> ArbPaymentEvent()
    {
        var paymentStatuses = Enum.GetValues<PaymentStatus>();

        return (from id in Arb.Generate<Guid>()
                from createdAt in UtcDateTimeGen
                from updatedAt in Gen.Choose(0, 365).Select(days => createdAt.AddDays(days))
                from eventId in NonEmptyAlphanumericGen
                from driverId in Arb.Generate<Guid>()
                from payerId in Arb.Generate<Guid>()
                from payerName in Gen.Elements("Juan Dela Cruz", "Maria Santos", "Pedro Reyes", "Ana Garcia", "Jose Rizal")
                from routeId in Arb.Generate<Guid>()
                from amountCentavos in Gen.Choose(100, 500000)
                from currency in Gen.Elements(Currencies)
                from status in Gen.Elements(paymentStatuses)
                from walletProvider in Gen.Elements(WalletProviders)
                from hasWalletTxId in Arb.Generate<bool>()
                from walletTxId in NonEmptyAlphanumericGen
                from occurredAt in UtcDateTimeGen
                from webhookTimestamp in UtcDateTimeGen
                from hasProcessedTimestamp in Arb.Generate<bool>()
                from processedTimestamp in UtcDateTimeGen
                from rawPayloadHash in HexHashGen
                let idempotencyKey = PaymentEvent.GenerateIdempotencyKey(eventId, driverId)
                select new PaymentEvent(
                    id, createdAt, updatedAt,
                    eventId, idempotencyKey,
                    driverId, payerId, payerName, routeId,
                    amountCentavos, currency, status,
                    walletProvider,
                    hasWalletTxId ? walletTxId : null,
                    occurredAt, webhookTimestamp,
                    hasProcessedTimestamp ? processedTimestamp : (DateTime?)null,
                    rawPayloadHash))
            .ToArbitrary();
    }

    /// <summary>
    /// **Validates: Requirements 3.10, 3.11**
    /// 
    /// Property 2: Round-trip PaymentEvent serialization.
    /// For all valid PaymentEvent entities, serializing, parsing, and re-serializing
    /// produces a dictionary semantically equivalent to the original serialization.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "biyahero-mvp")]
    [Trait("Property", "Property 2: Round-trip PaymentEvent serialization")]
    public Property RoundTrip_PaymentEvent_Serialization()
    {
        return Prop.ForAll(ArbPaymentEvent(), paymentEvent =>
        {
            // Step 1: Serialize the original PaymentEvent
            var serialized1 = paymentEvent.Serialize();

            // Step 2: Parse it back into a PaymentEvent
            var parsed = PaymentEvent.Parse(serialized1);

            // Step 3: Re-serialize the parsed PaymentEvent
            var serialized2 = parsed.Serialize();

            // Step 4: Assert the two serialized dictionaries are equivalent
            return DictionariesAreEquivalent(serialized1, serialized2)
                .Label("Serialized dictionaries should be equivalent after round-trip");
        });
    }

    /// <summary>
    /// Compares two dictionaries for semantic equivalence.
    /// </summary>
    private static bool DictionariesAreEquivalent(
        Dictionary<string, object?> dict1,
        Dictionary<string, object?> dict2)
    {
        if (dict1.Count != dict2.Count)
            return false;

        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var val2))
                return false;

            if (!ValuesAreEquivalent(kvp.Value, val2))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Compares two values for semantic equivalence, handling nulls and primitives.
    /// </summary>
    private static bool ValuesAreEquivalent(object? val1, object? val2)
    {
        if (val1 is null && val2 is null)
            return true;
        if (val1 is null || val2 is null)
            return false;

        // Handle numeric values with tolerance
        if (IsNumeric(val1) && IsNumeric(val2))
        {
            var d1 = Convert.ToDouble(val1);
            var d2 = Convert.ToDouble(val2);
            return Math.Abs(d1 - d2) < 0.0001;
        }

        // String comparison (handles DateTime round-trip formatting, Guid, enum)
        return val1.ToString() == val2.ToString();
    }

    private static bool IsNumeric(object? value)
    {
        return value is int or long or float or double or decimal;
    }
}
