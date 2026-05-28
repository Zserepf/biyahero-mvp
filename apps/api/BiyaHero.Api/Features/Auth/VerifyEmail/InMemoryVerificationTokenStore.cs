using System.Collections.Concurrent;

namespace BiyaHero.Api.Features.Auth.VerifyEmail;

/// <summary>
/// In-memory implementation of IVerificationTokenStore for MVP.
/// Tokens are stored in a thread-safe dictionary with expiry timestamps.
/// Can be replaced with Redis or DynamoDB for production use.
/// </summary>
public sealed class InMemoryVerificationTokenStore : IVerificationTokenStore
{
    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();

    /// <inheritdoc />
    public Task StoreTokenAsync(Guid userId, string token, TimeSpan expiry)
    {
        var entry = new TokenEntry(userId, DateTime.UtcNow.Add(expiry));
        _tokens[token] = entry;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Guid?> ValidateAndConsumeTokenAsync(string token)
    {
        if (!_tokens.TryRemove(token, out var entry))
        {
            return Task.FromResult<Guid?>(null);
        }

        // Token found but expired — treat as invalid
        if (entry.ExpiresAt < DateTime.UtcNow)
        {
            return Task.FromResult<Guid?>(null);
        }

        return Task.FromResult<Guid?>(entry.UserId);
    }

    private sealed record TokenEntry(Guid UserId, DateTime ExpiresAt);
}
