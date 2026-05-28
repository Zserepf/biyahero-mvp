using System.Security.Claims;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Auth.Me;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;
using System.Linq.Expressions;

namespace BiyaHero.Api.Tests.Features.Auth;

public class MeHandlerTests
{
    private readonly FakeJwtService _jwtService = new();
    private readonly FakeUserRepository _userRepository = new();
    private readonly MeHandler _handler;

    public MeHandlerTests()
    {
        _handler = new MeHandler(_userRepository, _jwtService);
    }

    // ─── GET /v1/auth/me ──────────────────────────────────────────────────

    [Fact]
    public async Task GetMeAsync_ValidToken_ReturnsUserProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(
            id: userId,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            email: "commuter@example.com",
            passwordHash: "hashed",
            role: UserRole.Commuter,
            status: UserStatus.Active,
            displayName: "Juan Dela Cruz",
            languagePreference: "fil");

        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, user.Email, "Commuter"));
        _userRepository.SetUser(user);

        // Act
        var response = await _handler.GetMeAsync("Bearer valid-token");

        // Assert
        Assert.Equal(userId, response.Id);
        Assert.Equal("commuter@example.com", response.Email);
        Assert.Equal("Commuter", response.Role);
        Assert.Equal("Juan Dela Cruz", response.DisplayName);
        Assert.Equal("fil", response.LanguagePreference);
    }

    [Fact]
    public async Task GetMeAsync_MissingAuthHeader_ThrowsUnauthenticatedException()
    {
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.GetMeAsync(null));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task GetMeAsync_EmptyAuthHeader_ThrowsUnauthenticatedException()
    {
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.GetMeAsync(""));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task GetMeAsync_NonBearerScheme_ThrowsUnauthenticatedException()
    {
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.GetMeAsync("Basic dXNlcjpwYXNz"));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task GetMeAsync_InvalidToken_ThrowsUnauthenticatedException()
    {
        _jwtService.SetValidationResult(JwtValidationResult.Failure("Token expired."));

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.GetMeAsync("Bearer expired-token"));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task GetMeAsync_UserNotFound_ThrowsUnauthenticatedException()
    {
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, "ghost@example.com", "Commuter"));
        _userRepository.SetUser(null); // user not in DB

        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.GetMeAsync("Bearer valid-token"));

        Assert.Equal(401, ex.StatusCode);
    }

    // ─── PATCH /v1/auth/me/language-preference ────────────────────────────

    [Theory]
    [InlineData("en")]
    [InlineData("fil")]
    public async Task UpdateLanguagePreferenceAsync_ValidLanguage_ReturnsUpdatedPreference(string lang)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(
            id: userId,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            email: "driver@example.com",
            passwordHash: "hashed",
            role: UserRole.Driver,
            status: UserStatus.Active,
            displayName: "Maria Santos",
            languagePreference: "fil");

        _jwtService.SetValidationResult(JwtValidationResult.Success(userId, user.Email, "Driver"));
        _userRepository.SetUser(user);

        var request = new UpdateLanguagePreferenceRequest(lang);

        // Act
        var response = await _handler.UpdateLanguagePreferenceAsync("Bearer valid-token", request);

        // Assert
        Assert.Equal(lang, response.LanguagePreference);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("fr")]
    [InlineData("tagalog")]
    [InlineData("EN_US")]
    public async Task UpdateLanguagePreferenceAsync_InvalidLanguage_ThrowsValidationException(string invalidLang)
    {
        var request = new UpdateLanguagePreferenceRequest(invalidLang);

        var ex = await Assert.ThrowsAsync<BiyaHero.Api.Features.Auth.Me.ValidationException>(
            () => _handler.UpdateLanguagePreferenceAsync("Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Equal("input.validation_failed", ex.Code);
    }

    [Fact]
    public async Task UpdateLanguagePreferenceAsync_NullLanguage_ThrowsValidationException()
    {
        var request = new UpdateLanguagePreferenceRequest(null!);

        var ex = await Assert.ThrowsAsync<BiyaHero.Api.Features.Auth.Me.ValidationException>(
            () => _handler.UpdateLanguagePreferenceAsync("Bearer valid-token", request));

        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateLanguagePreferenceAsync_MissingAuth_ThrowsUnauthenticatedException()
    {
        var request = new UpdateLanguagePreferenceRequest("en");

        // Validation passes but auth fails
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.UpdateLanguagePreferenceAsync(null, request));

        Assert.Equal(401, ex.StatusCode);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

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

    private sealed class FakeUserRepository : IUserRepository
    {
        private User? _user;

        public void SetUser(User? user) => _user = user;

        public Task<User?> FindByIdAsync(Guid id) => Task.FromResult(_user);
        public Task<User?> FindByEmailAsync(string email) => Task.FromResult(_user);
        public Task<bool> EmailExistsAsync(string email) => Task.FromResult(_user != null);

        public Task<User> UpdateLanguagePreferenceAsync(Guid userId, string languagePreference)
        {
            if (_user == null) throw new InvalidOperationException("User not found.");
            _user.LanguagePreference = languagePreference;
            return Task.FromResult(_user);
        }

        public Task<User> SuspendAsync(Guid userId)
        {
            if (_user == null) throw new InvalidOperationException("User not found.");
            _user.Status = UserStatus.Suspended;
            return Task.FromResult(_user);
        }

        public Task<User> ChangeRoleAsync(Guid userId, UserRole newRole)
        {
            if (_user == null) throw new InvalidOperationException("User not found.");
            _user.Role = newRole;
            return Task.FromResult(_user);
        }

        public Task<IReadOnlyList<User>> FindAllAsync() => Task.FromResult<IReadOnlyList<User>>(new List<User>());
        public Task<User> CreateAsync(User entity) => Task.FromResult(entity);
        public Task<IReadOnlyList<User>> WhereAsync(Expression<Func<User, bool>> predicate) => Task.FromResult<IReadOnlyList<User>>(new List<User>());
        public Task<User> SaveAsync(User entity) => Task.FromResult(entity);

        public Task<User> UpdateAsync(User entity)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(entity);
        }

        public Task DeleteAsync(User entity) => Task.CompletedTask;
    }
}
