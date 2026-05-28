using BiyaHero.Api.Features.Auth.VerifyEmail;

namespace BiyaHero.Api.Tests.Features.Auth;

public class InMemoryVerificationTokenStoreTests
{
    private readonly InMemoryVerificationTokenStore _store = new();

    [Fact]
    public async Task StoreAndValidate_ValidToken_ReturnsUserId()
    {
        var userId = Guid.NewGuid();
        var token = "test-token-abc";

        await _store.StoreTokenAsync(userId, token, TimeSpan.FromHours(1));
        var result = await _store.ValidateAndConsumeTokenAsync(token);

        Assert.Equal(userId, result);
    }

    [Fact]
    public async Task ValidateAndConsume_NonexistentToken_ReturnsNull()
    {
        var result = await _store.ValidateAndConsumeTokenAsync("does-not-exist");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAndConsume_ExpiredToken_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var token = "expired-token";

        // Store with negative expiry so it's already expired
        await _store.StoreTokenAsync(userId, token, TimeSpan.FromMilliseconds(-1));
        var result = await _store.ValidateAndConsumeTokenAsync(token);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAndConsume_TokenConsumedOnce_SecondCallReturnsNull()
    {
        var userId = Guid.NewGuid();
        var token = "one-time-token";

        await _store.StoreTokenAsync(userId, token, TimeSpan.FromHours(1));

        var first = await _store.ValidateAndConsumeTokenAsync(token);
        var second = await _store.ValidateAndConsumeTokenAsync(token);

        Assert.Equal(userId, first);
        Assert.Null(second);
    }

    [Fact]
    public async Task StoreToken_OverwritesSameTokenKey()
    {
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var token = "shared-token";

        await _store.StoreTokenAsync(userId1, token, TimeSpan.FromHours(1));
        await _store.StoreTokenAsync(userId2, token, TimeSpan.FromHours(1));

        var result = await _store.ValidateAndConsumeTokenAsync(token);

        // Last write wins
        Assert.Equal(userId2, result);
    }
}
