using System.Linq.Expressions;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Tests.Domain;

/// <summary>
/// Placeholder test entity that inherits BaseDomain.
/// Used to verify Serialize → Parse round-trip equivalence.
/// </summary>
public class TestEntity : BaseDomain
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public TestEntity() : base() { }

    public TestEntity(Guid id, DateTime createdAt, DateTime updatedAt,
        string name, decimal amount, double latitude, double longitude)
        : base(id, createdAt, updatedAt)
    {
        Name = name;
        Amount = amount;
        Latitude = latitude;
        Longitude = longitude;
    }

    public override Dictionary<string, object?> Serialize()
    {
        var dict = base.Serialize();
        dict["name"] = Name;
        dict["amount"] = Amount;
        dict["latitude"] = Latitude;
        dict["longitude"] = Longitude;
        return dict;
    }

    /// <summary>
    /// Parse a serialized dictionary back into a TestEntity instance.
    /// This is the inverse of Serialize() and enables round-trip verification.
    /// </summary>
    public static TestEntity Parse(Dictionary<string, object?> data)
    {
        var id = Guid.Parse(data["id"]?.ToString() ?? throw new ArgumentException("Missing id"));
        var createdAt = DateTime.Parse(data["createdAt"]?.ToString() ?? throw new ArgumentException("Missing createdAt"));
        var updatedAt = DateTime.Parse(data["updatedAt"]?.ToString() ?? throw new ArgumentException("Missing updatedAt"));
        var name = data["name"]?.ToString() ?? string.Empty;
        var amount = decimal.Parse(data["amount"]?.ToString() ?? "0");
        var latitude = double.Parse(data["latitude"]?.ToString() ?? "0");
        var longitude = double.Parse(data["longitude"]?.ToString() ?? "0");

        return new TestEntity(id, createdAt, updatedAt, name, amount, latitude, longitude);
    }
}

/// <summary>
/// Unit tests for BaseDomain.Serialize round-trip property.
/// Validates: Requirements 1.11, 3.11
/// Verifies that Serialize → Parse produces an equivalent entity.
/// </summary>
public class BaseDomainSerializeTests
{
    [Fact]
    public void Serialize_ThenParse_ProducesEquivalentEntity()
    {
        // Arrange
        var original = new TestEntity(
            id: Guid.NewGuid(),
            createdAt: new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc),
            updatedAt: new DateTime(2024, 6, 15, 11, 0, 0, DateTimeKind.Utc),
            name: "Test Route",
            amount: 15.75m,
            latitude: 14.5995,
            longitude: 120.9842
        );

        // Act
        var serialized = original.Serialize();
        var parsed = TestEntity.Parse(serialized);

        // Assert
        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.CreatedAt, parsed.CreatedAt);
        Assert.Equal(original.UpdatedAt, parsed.UpdatedAt);
        Assert.Equal(original.Name, parsed.Name);
        Assert.Equal(original.Amount, parsed.Amount);
        Assert.Equal(original.Latitude, parsed.Latitude);
        Assert.Equal(original.Longitude, parsed.Longitude);
    }

    [Fact]
    public void Serialize_ThenParse_ThenReserialize_ProducesSameJson()
    {
        // Arrange
        var original = new TestEntity(
            id: Guid.NewGuid(),
            createdAt: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            updatedAt: new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            name: "Manila-Cubao Express",
            amount: 13.00m,
            latitude: 14.6507,
            longitude: 121.0495
        );

        // Act: Serialize → Parse → Re-serialize
        var firstSerialization = original.Serialize();
        var parsed = TestEntity.Parse(firstSerialization);
        var secondSerialization = parsed.Serialize();

        // Assert: Both serializations are semantically equivalent
        Assert.Equal(firstSerialization.Count, secondSerialization.Count);
        foreach (var key in firstSerialization.Keys)
        {
            Assert.True(secondSerialization.ContainsKey(key), $"Key '{key}' missing in re-serialized output");
            Assert.Equal(firstSerialization[key]?.ToString(), secondSerialization[key]?.ToString());
        }
    }

    [Fact]
    public void SerializeToJson_ProducesValidJsonString()
    {
        // Arrange
        var entity = new TestEntity(
            id: Guid.Parse("12345678-1234-1234-1234-123456789abc"),
            createdAt: new DateTime(2024, 3, 10, 8, 0, 0, DateTimeKind.Utc),
            updatedAt: new DateTime(2024, 3, 10, 9, 0, 0, DateTimeKind.Utc),
            name: "Test",
            amount: 10.50m,
            latitude: 14.0,
            longitude: 121.0
        );

        // Act
        var json = entity.SerializeToJson();

        // Assert: JSON is non-empty and contains expected keys
        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("\"id\"", json);
        Assert.Contains("\"createdAt\"", json);
        Assert.Contains("\"updatedAt\"", json);
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"amount\"", json);
    }

    [Fact]
    public void Serialize_BaseProperties_AreAlwaysPresent()
    {
        // Arrange
        var entity = new TestEntity();

        // Act
        var serialized = entity.Serialize();

        // Assert: Base properties always present
        Assert.True(serialized.ContainsKey("id"));
        Assert.True(serialized.ContainsKey("createdAt"));
        Assert.True(serialized.ContainsKey("updatedAt"));
        Assert.NotNull(serialized["id"]);
        Assert.NotNull(serialized["createdAt"]);
        Assert.NotNull(serialized["updatedAt"]);
    }

    [Fact]
    public void Serialize_DefaultConstructor_GeneratesValidId()
    {
        // Arrange & Act
        var entity = new TestEntity();
        var serialized = entity.Serialize();

        // Assert
        var parsedId = Guid.Parse(serialized["id"]!.ToString()!);
        Assert.NotEqual(Guid.Empty, parsedId);
    }
}
