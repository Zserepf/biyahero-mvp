using BiyaHero.Api.Services;

namespace BiyaHero.Api.Tests.Services;

public class Argon2idPasswordHasherTests
{
    private readonly Argon2idPasswordHasher _hasher = new();

    [Fact]
    public void Hash_ProducesValidArgon2idFormatString()
    {
        var hash = _hasher.Hash("MySecurePassword123!");

        // Format: $argon2id$v=19$m=65536,t=3,p=1$<base64-salt>$<base64-hash>
        Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=1$", hash);

        var parts = hash.Split('$');
        Assert.Equal(6, parts.Length);
        Assert.Equal("", parts[0]);
        Assert.Equal("argon2id", parts[1]);
        Assert.Equal("v=19", parts[2]);
        Assert.Equal("m=65536,t=3,p=1", parts[3]);

        // Salt should be valid base64 (16 bytes = 24 chars base64)
        var saltBytes = Convert.FromBase64String(parts[4]);
        Assert.Equal(16, saltBytes.Length);

        // Hash should be valid base64 (32 bytes = 44 chars base64)
        var hashBytes = Convert.FromBase64String(parts[5]);
        Assert.Equal(32, hashBytes.Length);
    }

    [Fact]
    public void Verify_ReturnsTrueForCorrectPassword()
    {
        var password = "CorrectHorseBatteryStaple";
        var hash = _hasher.Hash(password);

        var result = _hasher.Verify(password, hash);

        Assert.True(result);
    }

    [Fact]
    public void Verify_ReturnsFalseForWrongPassword()
    {
        var hash = _hasher.Hash("OriginalPassword");

        var result = _hasher.Verify("WrongPassword", hash);

        Assert.False(result);
    }

    [Fact]
    public void Hash_DifferentPasswordsProduceDifferentHashes()
    {
        var hash1 = _hasher.Hash("PasswordOne");
        var hash2 = _hasher.Hash("PasswordTwo");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_SamePasswordProducesDifferentHashes_RandomSalt()
    {
        var password = "SamePasswordEachTime";
        var hash1 = _hasher.Hash(password);
        var hash2 = _hasher.Hash(password);

        // Full hash strings differ due to random salt
        Assert.NotEqual(hash1, hash2);

        // But both should still verify correctly
        Assert.True(_hasher.Verify(password, hash1));
        Assert.True(_hasher.Verify(password, hash2));
    }
}
