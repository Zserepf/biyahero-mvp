using System.Security.Cryptography;
using System.Text;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BiyaHero.Api.Tests.Services;

public class JwtServiceTests
{
    private static readonly byte[] TestSigningKey = RandomNumberGenerator.GetBytes(32);
    private static readonly DateTimeOffset FixedNow = new(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeSecretService _secretService = new();
    private readonly FakeTimeProvider _timeProvider = new(FixedNow);
    private readonly JwtService _jwtService;

    public JwtServiceTests()
    {
        _jwtService = new JwtService(_secretService, _timeProvider);
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ContainsCorrectClaims()
    {
        var user = CreateTestUser();

        var token = await _jwtService.GenerateAccessTokenAsync(user);

        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        Assert.Equal(user.Id.ToString(), jwt.GetClaim(JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(user.Role.ToString(), jwt.GetClaim("role").Value);

        // Verify 24-hour expiry
        var exp = jwt.ValidTo;
        var iat = jwt.IssuedAt;
        Assert.Equal(TimeSpan.FromHours(24), exp - iat);
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_Has30DayExpiry()
    {
        var user = CreateTestUser();

        var token = await _jwtService.GenerateRefreshTokenAsync(user);

        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        Assert.Equal(user.Id.ToString(), jwt.GetClaim(JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("refresh", jwt.GetClaim("type").Value);

        // Verify 30-day expiry
        var exp = jwt.ValidTo;
        var iat = jwt.IssuedAt;
        Assert.Equal(TimeSpan.FromDays(30), exp - iat);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsClaimsPrincipal_ForFreshlyIssuedAccessToken()
    {
        var user = CreateTestUser();
        var token = await _jwtService.GenerateAccessTokenAsync(user);

        var principal = await _jwtService.ValidateTokenAsync(token);

        Assert.NotNull(principal);
        var subClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Assert.Equal(user.Id.ToString(), subClaim);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsNull_ForExpiredToken()
    {
        var user = CreateTestUser();
        var token = await _jwtService.GenerateAccessTokenAsync(user);

        // Advance time past the 24-hour expiry
        var expiredTimeProvider = new FakeTimeProvider(FixedNow.AddHours(25));
        var expiredService = new JwtService(_secretService, expiredTimeProvider);

        var principal = await expiredService.ValidateTokenAsync(token);

        Assert.Null(principal);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsNull_ForTamperedToken()
    {
        var user = CreateTestUser();
        var token = await _jwtService.GenerateAccessTokenAsync(user);

        // Tamper with the token by modifying the payload
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        // Flip a character in the payload
        var tamperedPayload = parts[1][..^1] + (parts[1][^1] == 'A' ? 'B' : 'A');
        var tamperedToken = $"{parts[0]}.{tamperedPayload}.{parts[2]}";

        var principal = await _jwtService.ValidateTokenAsync(tamperedToken);

        Assert.Null(principal);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsNull_ForEmptyToken()
    {
        var principal = await _jwtService.ValidateTokenAsync("");

        Assert.Null(principal);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsClaimsPrincipal_ForFreshlyIssuedRefreshToken()
    {
        var user = CreateTestUser();
        var token = await _jwtService.GenerateRefreshTokenAsync(user);

        var principal = await _jwtService.ValidateTokenAsync(token);

        Assert.NotNull(principal);
    }

    [Fact]
    public async Task GetUserIdFromTokenAsync_ReturnsUserId_ForValidToken()
    {
        var user = CreateTestUser();
        var token = await _jwtService.GenerateAccessTokenAsync(user);

        var userId = await _jwtService.GetUserIdFromTokenAsync(token);

        Assert.NotNull(userId);
        Assert.Equal(user.Id, userId.Value);
    }

    [Fact]
    public async Task GetUserIdFromTokenAsync_ReturnsNull_ForInvalidToken()
    {
        var userId = await _jwtService.GetUserIdFromTokenAsync("invalid-token");

        Assert.Null(userId);
    }

    [Fact]
    public async Task GetUserIdFromTokenAsync_ReturnsNull_ForExpiredToken()
    {
        var user = CreateTestUser();
        var token = await _jwtService.GenerateAccessTokenAsync(user);

        // Advance time past the 24-hour expiry
        var expiredTimeProvider = new FakeTimeProvider(FixedNow.AddHours(25));
        var expiredService = new JwtService(_secretService, expiredTimeProvider);

        var userId = await expiredService.GetUserIdFromTokenAsync(token);

        Assert.Null(userId);
    }

    [Fact]
    public async Task ValidateTokenDetailedAsync_ReturnsValid_ForFreshlyIssuedAccessToken()
    {
        var user = CreateTestUser();
        var token = await _jwtService.GenerateAccessTokenAsync(user);

        var result = await _jwtService.ValidateTokenDetailedAsync(token);

        Assert.True(result.IsValid);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.Role.ToString(), result.Role);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateTokenDetailedAsync_ReturnsInvalid_ForExpiredToken()
    {
        var user = CreateTestUser();
        var token = await _jwtService.GenerateAccessTokenAsync(user);

        // Advance time past the 24-hour expiry
        var expiredTimeProvider = new FakeTimeProvider(FixedNow.AddHours(25));
        var expiredService = new JwtService(_secretService, expiredTimeProvider);

        var result = await expiredService.ValidateTokenDetailedAsync(token);

        Assert.False(result.IsValid);
        Assert.Contains("expired", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateTokenDetailedAsync_ReturnsInvalid_ForEmptyToken()
    {
        var result = await _jwtService.ValidateTokenDetailedAsync("");

        Assert.False(result.IsValid);
        Assert.Contains("missing", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateTokenDetailedAsync_ReturnsRefreshTokenType()
    {
        var user = CreateTestUser();
        var token = await _jwtService.GenerateRefreshTokenAsync(user);

        var result = await _jwtService.ValidateTokenDetailedAsync(token);

        Assert.True(result.IsValid);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("refresh", result.TokenType);
        Assert.Null(result.Email); // Refresh tokens don't carry email
        Assert.Null(result.Role);  // Refresh tokens don't carry role
    }

    private static User CreateTestUser() => new(
        id: Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
        createdAt: DateTime.UtcNow,
        updatedAt: DateTime.UtcNow,
        email: "rider@biyahero.ph",
        passwordHash: "hashed",
        role: UserRole.Commuter,
        status: UserStatus.Active,
        displayName: "Test Rider",
        languagePreference: "fil");

    private sealed class FakeSecretService : ISecretService
    {
        public Task<byte[]> GetJwtSigningKeyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(TestSigningKey);

        public Task<byte[]> GetWebhookSigningSecretAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Encoding.UTF8.GetBytes("webhook-secret"));
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
