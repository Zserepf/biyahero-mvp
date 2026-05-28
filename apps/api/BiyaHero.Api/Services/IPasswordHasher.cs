namespace BiyaHero.Api.Services;

/// <summary>
/// Provides password hashing and verification using a memory-hard algorithm.
/// Implementations must be thread-safe for concurrent invocations.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password using Argon2id with a random salt.
    /// Returns the encoded hash string in PHC format.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Verifies a plaintext password against a previously computed hash.
    /// Returns true if the password matches, false otherwise.
    /// </summary>
    bool Verify(string password, string hash);
}
