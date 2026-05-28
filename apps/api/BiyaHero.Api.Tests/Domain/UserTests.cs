using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Tests.Domain;

/// <summary>
/// Unit tests for the User domain class.
/// Validates: Requirements 5.1, 5.2, 10.1, 10.3
/// </summary>
public class UserTests
{
    [Fact]
    public void Constructor_Default_CreatesUserWithDefaults()
    {
        var user = new User();

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(UserRole.Commuter, user.Role);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
        Assert.Equal("fil", user.LanguagePreference);
        Assert.Equal(string.Empty, user.Email);
        Assert.Equal(string.Empty, user.DisplayName);
    }

    [Fact]
    public void Constructor_Full_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2024, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2024, 6, 2, 12, 0, 0, DateTimeKind.Utc);

        var user = new User(id, createdAt, updatedAt,
            "driver@biyahero.ph", "hashed_pw", UserRole.Driver,
            UserStatus.Active, "Juan dela Cruz", "en");

        Assert.Equal(id, user.Id);
        Assert.Equal(createdAt, user.CreatedAt);
        Assert.Equal(updatedAt, user.UpdatedAt);
        Assert.Equal("driver@biyahero.ph", user.Email);
        Assert.Equal("hashed_pw", user.PasswordHash);
        Assert.Equal(UserRole.Driver, user.Role);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal("Juan dela Cruz", user.DisplayName);
        Assert.Equal("en", user.LanguagePreference);
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("fil", "fil")]
    [InlineData("EN", "en")]
    [InlineData("FIL", "fil")]
    [InlineData("En", "en")]
    public void LanguagePreference_ValidValues_Accepted(string input, string expected)
    {
        var user = new User { LanguagePreference = input };
        Assert.Equal(expected, user.LanguagePreference);
    }

    [Theory]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("tl")]
    [InlineData("tagalog")]
    [InlineData("english")]
    [InlineData("jp")]
    public void LanguagePreference_InvalidValues_DefaultToFil(string input)
    {
        var user = new User { LanguagePreference = input };
        Assert.Equal("fil", user.LanguagePreference);
    }

    [Fact]
    public void Serialize_IncludesAllPublicFieldsExceptPasswordHash()
    {
        var user = new User(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow,
            "commuter@test.com",
            "secret_hash",
            UserRole.Commuter,
            UserStatus.Active,
            "Maria Santos",
            "fil");

        var serialized = user.Serialize();

        Assert.True(serialized.ContainsKey("id"));
        Assert.True(serialized.ContainsKey("createdAt"));
        Assert.True(serialized.ContainsKey("updatedAt"));
        Assert.True(serialized.ContainsKey("email"));
        Assert.True(serialized.ContainsKey("role"));
        Assert.True(serialized.ContainsKey("status"));
        Assert.True(serialized.ContainsKey("displayName"));
        Assert.True(serialized.ContainsKey("languagePreference"));
        // PasswordHash must NOT be serialized
        Assert.False(serialized.ContainsKey("passwordHash"));
    }

    [Fact]
    public void Serialize_ThenParse_ProducesEquivalentUser()
    {
        var original = new User(
            Guid.NewGuid(),
            new DateTime(2024, 5, 20, 8, 30, 0, DateTimeKind.Utc),
            new DateTime(2024, 5, 20, 9, 0, 0, DateTimeKind.Utc),
            "mod@biyahero.ph",
            "argon2id_hash",
            UserRole.Moderator,
            UserStatus.Active,
            "Admin Mod",
            "en");

        var serialized = original.Serialize();
        var parsed = User.Parse(serialized);

        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.CreatedAt, parsed.CreatedAt);
        Assert.Equal(original.UpdatedAt, parsed.UpdatedAt);
        Assert.Equal(original.Email, parsed.Email);
        Assert.Equal(original.Role, parsed.Role);
        Assert.Equal(original.Status, parsed.Status);
        Assert.Equal(original.DisplayName, parsed.DisplayName);
        Assert.Equal(original.LanguagePreference, parsed.LanguagePreference);
        // PasswordHash is not serialized, so parsed user has empty hash
        Assert.Equal(string.Empty, parsed.PasswordHash);
    }

    [Fact]
    public void Serialize_ThenParse_ThenReserialize_ProducesSameOutput()
    {
        var original = new User(
            Guid.NewGuid(),
            new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 16, 12, 0, 0, DateTimeKind.Utc),
            "superadmin@biyahero.ph",
            "hash",
            UserRole.SuperAdmin,
            UserStatus.Active,
            "Super Admin",
            "fil");

        var firstSerialization = original.Serialize();
        var parsed = User.Parse(firstSerialization);
        var secondSerialization = parsed.Serialize();

        Assert.Equal(firstSerialization.Count, secondSerialization.Count);
        foreach (var key in firstSerialization.Keys)
        {
            Assert.True(secondSerialization.ContainsKey(key), $"Key '{key}' missing in re-serialized output");
            Assert.Equal(firstSerialization[key]?.ToString(), secondSerialization[key]?.ToString());
        }
    }

    [Theory]
    [InlineData(UserRole.Commuter)]
    [InlineData(UserRole.Driver)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SuperAdmin)]
    public void Serialize_ThenParse_PreservesAllRoles(UserRole role)
    {
        var user = new User { Role = role, Email = "test@test.com", DisplayName = "Test" };

        var serialized = user.Serialize();
        var parsed = User.Parse(serialized);

        Assert.Equal(role, parsed.Role);
    }

    [Theory]
    [InlineData(UserStatus.PendingVerification)]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.Suspended)]
    public void Serialize_ThenParse_PreservesAllStatuses(UserStatus status)
    {
        var user = new User { Status = status, Email = "test@test.com", DisplayName = "Test" };

        var serialized = user.Serialize();
        var parsed = User.Parse(serialized);

        Assert.Equal(status, parsed.Status);
    }
}
