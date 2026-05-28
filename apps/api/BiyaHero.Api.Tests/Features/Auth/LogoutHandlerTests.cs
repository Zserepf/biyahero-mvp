using System.Security.Claims;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Auth.Logout;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Tests.Features.Auth;

public class LogoutHandlerTests
{
    private readonly FakeJwtService _jwtService = new();
    private readonly LogoutHandler _handler;

    public LogoutHandlerTests()
    {
        _handler = new LogoutHandler(_jwtService);
    }

    [Fact]
    public async Task HandleAsync_ValidToken_CompletesWithoutException()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Success(Guid.NewGuid(), "user@test.com", "Commuter"));

        // Act & Assert — should not throw
        await _handler.HandleAsync("Bearer valid-token-123");
    }

    [Fact]
    public async Task HandleAsync_InvalidToken_ThrowsUnauthenticatedException()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Failure("Token expired."));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync("Bearer expired-token"));

        Assert.Equal("Invalid or expired token.", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_MissingAuthorizationHeader_ThrowsUnauthenticatedException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(null));

        Assert.Equal("Authentication required.", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_EmptyAuthorizationHeader_ThrowsUnauthenticatedException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(""));

        Assert.Equal("Authentication required.", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_NonBearerScheme_ThrowsUnauthenticatedException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync("Basic dXNlcjpwYXNz"));

        Assert.Equal("Authentication required.", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_BearerWithEmptyToken_ThrowsUnauthenticatedException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync("Bearer    "));

        Assert.Equal("Authentication required.", ex.Message);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal fake JWT service for testing the handler in isolation.
    /// </summary>
    private sealed class FakeJwtService : IJwtService
    {
        private JwtValidationResult _validationResult = JwtValidationResult.Failure("Not configured.");

        public void SetValidationResult(JwtValidationResult result) => _validationResult = result;

        public Task<string> GenerateAccessTokenAsync(User user, CancellationToken cancellationToken = default)
            => Task.FromResult("fake-access-token");

        public Task<string> GenerateRefreshTokenAsync(User user, CancellationToken cancellationToken = default)
            => Task.FromResult("fake-refresh-token");

        public Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (_validationResult.IsValid)
            {
                var claims = new List<Claim>
                {
                    new("sub", _validationResult.UserId?.ToString() ?? ""),
                };
                if (_validationResult.Email != null)
                    claims.Add(new("email", _validationResult.Email));
                if (_validationResult.Role != null)
                    claims.Add(new("role", _validationResult.Role));

                var identity = new ClaimsIdentity(claims, "Test");
                return Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(identity));
            }
            return Task.FromResult<ClaimsPrincipal?>(null);
        }

        public Task<Guid?> GetUserIdFromTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (_validationResult.IsValid)
                return Task.FromResult<Guid?>(_validationResult.UserId);
            return Task.FromResult<Guid?>(null);
        }

        public Task<JwtValidationResult> ValidateTokenDetailedAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(_validationResult);
    }
}
