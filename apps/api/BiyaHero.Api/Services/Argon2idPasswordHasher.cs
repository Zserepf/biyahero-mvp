using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace BiyaHero.Api.Services;

/// <summary>
/// Argon2id password hasher with OWASP-recommended parameters.
/// Memory: 65536 KB (64 MiB), Iterations: 3, Parallelism: 1.
/// Output format: $argon2id$v=19$m=65536,t=3,p=1$&lt;base64-salt&gt;$&lt;base64-hash&gt;
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int MemorySize = 65536; // 64 MiB in KB
    private const int Iterations = 3;
    private const int Parallelism = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <inheritdoc />
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt);

        var saltBase64 = Convert.ToBase64String(salt);
        var hashBase64 = Convert.ToBase64String(hash);

        return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={Parallelism}${saltBase64}${hashBase64}";
    }

    /// <inheritdoc />
    public bool Verify(string password, string hash)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(hash);

        if (!TryParseHash(hash, out var memory, out var iterations, out var parallelism, out var salt, out var expectedHash))
        {
            return false;
        }

        var computedHash = ComputeHash(password, salt, memory, iterations, parallelism);

        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    private static byte[] ComputeHash(string password, byte[] salt, int memory = MemorySize, int iterations = Iterations, int parallelism = Parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.MemorySize = memory;
        argon2.Iterations = iterations;
        argon2.DegreeOfParallelism = parallelism;

        return argon2.GetBytes(HashSize);
    }

    private static bool TryParseHash(string encodedHash, out int memory, out int iterations, out int parallelism, out byte[] salt, out byte[] hash)
    {
        memory = 0;
        iterations = 0;
        parallelism = 0;
        salt = Array.Empty<byte>();
        hash = Array.Empty<byte>();

        // Expected format: $argon2id$v=19$m=65536,t=3,p=1$<base64-salt>$<base64-hash>
        var parts = encodedHash.Split('$');
        if (parts.Length != 6)
            return false;

        // parts[0] = "" (before first $)
        // parts[1] = "argon2id"
        // parts[2] = "v=19"
        // parts[3] = "m=65536,t=3,p=1"
        // parts[4] = base64 salt
        // parts[5] = base64 hash

        if (parts[1] != "argon2id")
            return false;

        if (!parts[2].StartsWith("v="))
            return false;

        // Parse parameters
        var paramParts = parts[3].Split(',');
        if (paramParts.Length != 3)
            return false;

        foreach (var param in paramParts)
        {
            var kv = param.Split('=');
            if (kv.Length != 2)
                return false;

            if (!int.TryParse(kv[1], out var value))
                return false;

            switch (kv[0])
            {
                case "m":
                    memory = value;
                    break;
                case "t":
                    iterations = value;
                    break;
                case "p":
                    parallelism = value;
                    break;
                default:
                    return false;
            }
        }

        try
        {
            salt = Convert.FromBase64String(parts[4]);
            hash = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }
}
