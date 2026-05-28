using System.Linq.Expressions;
using System.Security.Claims;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Auth.Refresh;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Tests.Features.Auth;

/// <summary>
/// Unit tests for RefreshHandler.
/// Validates: Requirements 5.3
/// - Valid refresh token issues new rotated tokens
/// - Invalid/expired refresh token returns 401
/// - Access token used as refresh token returns 401
/// - Suspended user returns 403
/// - Missing user returns 401
/// </summary>
public class RefreshHandlerTests
{
    private readonly FakeJwtService _jwtService = new();
    private readonly FakeUserRepository _userRepository = new();
    private readonly RefreshHandler _handler;

    public RefreshHandlerTests()
    {
        _handler = new RefreshHandler(_jwtService, _userRepository);
    }

    // ─── Successful Token Refresh ─────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidRefreshToken_ReturnsNewTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateActiveUser(userId, "user@example.com", UserRole.Commuter);
        _userRepository.AddUser(user);

        _jwtService.SetValidationResult(
            JwtValidationResult.Success(userId, "user@example.com", "Commuter", tokenType: "refresh"));

        var request = new RefreshRequest("valid-refresh-token");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        Assert.Equal("fake-access-token", response.AccessToken);
        Assert.Equal("fake-refresh-token", response.RefreshToken);
        Assert.Equal(86400, response.ExpiresIn);
    }

    [Fact]
    public async Task HandleAsync_ValidRefreshToken_DriverRole_ReturnsNewTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateActiveUser(userId, "driver@example.com", UserRole.Driver);
        _userRepository.AddUser(user);

        _jwtService.SetValidationResult(
            JwtValidationResult.Success(userId, "driver@example.com", "Driver", tokenType: "refresh"));

        var request = new RefreshRequest("valid-refresh-token");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        Assert.NotNull(response.AccessToken);
        Assert.NotNull(response.RefreshToken);
    }

    // ─── Invalid/Expired Refresh Token ────────────────────────────────────

    [Fact]
    public async Task HandleAsync_InvalidRefreshToken_ThrowsUnauthenticatedException()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Failure("Token is invalid."));

        var request = new RefreshRequest("invalid-token");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(request));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("Invalid or expired refresh token.", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_ExpiredRefreshToken_ThrowsUnauthenticatedException()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Failure("Token has expired."));

        var request = new RefreshRequest("expired-refresh-token");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(request));

        Assert.Equal(401, ex.StatusCode);
    }

    // ─── Wrong Token Type (Access Token Used as Refresh) ──────────────────

    [Fact]
    public async Task HandleAsync_AccessTokenUsedAsRefresh_ThrowsUnauthenticatedException()
    {
        // Arrange — token is valid but has type "access" instead of "refresh"
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(userId, "user@example.com", "Commuter", tokenType: "access"));

        var request = new RefreshRequest("access-token-not-refresh");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(request));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("Invalid token type.", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_NullTokenType_ThrowsUnauthenticatedException()
    {
        // Arrange — token is valid but has no type claim
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(userId, "user@example.com", "Commuter", tokenType: null));

        var request = new RefreshRequest("token-without-type");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(request));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("Invalid token type.", ex.Message);
    }

    // ─── User Not Found ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_UserNotFound_ThrowsUnauthenticatedException()
    {
        // Arrange — token is valid but user no longer exists in DB
        var userId = Guid.NewGuid();
        _jwtService.SetValidationResult(
            JwtValidationResult.Success(userId, "deleted@example.com", "Commuter", tokenType: "refresh"));
        // Don't add user to repository

        var request = new RefreshRequest("valid-refresh-token");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(request));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("User not found.", ex.Message);
    }

    // ─── Suspended User ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_SuspendedUser_ThrowsForbiddenException()
    {
        // Arrange — user exists but is suspended
        var userId = Guid.NewGuid();
        var user = new User(
            id: userId,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            email: "suspended@example.com",
            passwordHash: "hashed",
            role: UserRole.Commuter,
            status: UserStatus.Suspended,
            displayName: "Suspended User",
            languagePreference: "fil");
        _userRepository.AddUser(user);

        _jwtService.SetValidationResult(
            JwtValidationResult.Success(userId, "suspended@example.com", "Commuter", tokenType: "refresh"));

        var request = new RefreshRequest("valid-refresh-token");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.HandleAsync(request));

        Assert.Equal(403, ex.StatusCode);
        Assert.Contains("suspended", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static User CreateActiveUser(Guid id, string email, UserRole role) => new(
        id: id,
        createdAt: DateTime.UtcNow,
        updatedAt: DateTime.UtcNow,
        email: email,
        passwordHash: "hashed-password",
        role: role,
        status: UserStatus.Active,
        displayName: "Test User",
        languagePreference: "fil");

    // ─── Fakes ────────────────────────────────────────────────────────────

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
        private readonly Dictionary<Guid, User> _users = new();

        public void AddUser(User user) => _users[user.Id] = user;

        public Task<User?> FindByIdAsync(Guid id)
            => Task.FromResult(_users.GetValueOrDefault(id));

        public Task<User?> FindByEmailAsync(string email)
            => Task.FromResult(_users.Values.FirstOrDefault(u =>
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> EmailExistsAsync(string email)
            => Task.FromResult(_users.Values.Any(u =>
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<User>> FindAllAsync()
            => Task.FromResult<IReadOnlyList<User>>(_users.Values.ToList().AsReadOnly());

        public Task<User> CreateAsync(User entity)
        {
            _users[entity.Id] = entity;
            return Task.FromResult(entity);
        }

        public Task<IReadOnlyList<User>> WhereAsync(Expression<Func<User, bool>> predicate)
        {
            var compiled = predicate.Compile();
            var results = _users.Values.Where(compiled).ToList().AsReadOnly();
            return Task.FromResult<IReadOnlyList<User>>(results);
        }

        public Task<User> SaveAsync(User entity)
        {
            _users[entity.Id] = entity;
            return Task.FromResult(entity);
        }

        public Task<User> UpdateAsync(User entity)
        {
            _users[entity.Id] = entity;
            return Task.FromResult(entity);
        }

        public Task DeleteAsync(User entity)
        {
            _users.Remove(entity.Id);
            return Task.CompletedTask;
        }

        public Task<User> UpdateLanguagePreferenceAsync(Guid userId, string languagePreference)
        {
            if (!_users.TryGetValue(userId, out var user))
                throw new InvalidOperationException($"User with id {userId} not found.");
            user.LanguagePreference = languagePreference;
            return Task.FromResult(user);
        }

        public Task<User> SuspendAsync(Guid userId)
        {
            if (!_users.TryGetValue(userId, out var user))
                throw new InvalidOperationException($"User with id {userId} not found.");
            user.Status = UserStatus.Suspended;
            return Task.FromResult(user);
        }

        public Task<User> ChangeRoleAsync(Guid userId, UserRole newRole)
        {
            if (!_users.TryGetValue(userId, out var user))
                throw new InvalidOperationException($"User with id {userId} not found.");
            user.Role = newRole;
            return Task.FromResult(user);
        }
    }
}
