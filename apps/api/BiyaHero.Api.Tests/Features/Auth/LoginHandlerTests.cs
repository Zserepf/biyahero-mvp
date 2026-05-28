using System.Linq.Expressions;
using System.Security.Claims;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Auth.Login;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Tests.Features.Auth;

/// <summary>
/// Unit tests for LoginHandler.
/// Validates: Requirements 5.3, 5.6
/// - Generic 401 for unknown email (no info leak)
/// - Generic 401 for wrong password (no info leak)
/// - Generic 401 for non-active accounts (pending/suspended)
/// - Successful login returns JWT tokens and user info
/// </summary>
public class LoginHandlerTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeJwtService _jwtService = new();
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _handler = new LoginHandler(_userRepository, _passwordHasher, _jwtService);
    }

    // ─── Successful Login ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidCredentials_ReturnsTokensAndUserInfo()
    {
        // Arrange
        var user = CreateActiveUser("commuter@example.com", "SecurePass123", UserRole.Commuter);
        _userRepository.AddUser(user);

        var request = new LoginRequest("commuter@example.com", "SecurePass123");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        Assert.Equal("fake-access-token", response.AccessToken);
        Assert.Equal("fake-refresh-token", response.RefreshToken);
        Assert.Equal(86400, response.ExpiresIn);
        Assert.Equal(user.Id, response.User.Id);
        Assert.Equal("commuter@example.com", response.User.Email);
        Assert.Equal("Commuter", response.User.Role);
        Assert.Equal("Test User", response.User.DisplayName);
        Assert.Equal("fil", response.User.LanguagePreference);
    }

    [Fact]
    public async Task HandleAsync_DriverRole_ReturnsCorrectRoleInResponse()
    {
        // Arrange
        var user = CreateActiveUser("driver@example.com", "DriverPass1", UserRole.Driver);
        _userRepository.AddUser(user);

        var request = new LoginRequest("driver@example.com", "DriverPass1");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        Assert.Equal("Driver", response.User.Role);
    }

    [Fact]
    public async Task HandleAsync_ValidCredentials_ReturnsUserLanguagePreference()
    {
        // Arrange
        var user = new User(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            email: "english@example.com",
            passwordHash: "hashed:MyPass123",
            role: UserRole.Commuter,
            status: UserStatus.Active,
            displayName: "English User",
            languagePreference: "en");
        _userRepository.AddUser(user);

        var request = new LoginRequest("english@example.com", "MyPass123");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        Assert.Equal("en", response.User.LanguagePreference);
    }

    // ─── Generic 401 — Unknown Email (Req 5.6) ───────────────────────────

    [Fact]
    public async Task HandleAsync_UnknownEmail_ThrowsUnauthenticatedWithGenericMessage()
    {
        // Arrange — no users in repository
        var request = new LoginRequest("nonexistent@example.com", "AnyPassword1");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(request));

        // Req 5.6: generic message that does not distinguish "unknown email" from "wrong password"
        Assert.Equal("Invalid credentials.", ex.Message);
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("auth.unauthenticated", ex.Code);
    }

    // ─── Generic 401 — Wrong Password (Req 5.6) ──────────────────────────

    [Fact]
    public async Task HandleAsync_WrongPassword_ThrowsUnauthenticatedWithGenericMessage()
    {
        // Arrange
        var user = CreateActiveUser("user@example.com", "CorrectPassword", UserRole.Commuter);
        _userRepository.AddUser(user);

        var request = new LoginRequest("user@example.com", "WrongPassword");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(request));

        // Same generic message as unknown email — no info leak
        Assert.Equal("Invalid credentials.", ex.Message);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_UnknownEmailAndWrongPassword_SameErrorMessage()
    {
        // Arrange — create a user so we can compare error messages
        var user = CreateActiveUser("exists@example.com", "RealPassword", UserRole.Commuter);
        _userRepository.AddUser(user);

        // Act — unknown email
        var unknownEmailEx = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(new LoginRequest("ghost@example.com", "AnyPass")));

        // Act — wrong password
        var wrongPasswordEx = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(new LoginRequest("exists@example.com", "WrongPass")));

        // Assert — identical error messages (Req 5.6: no info leak)
        Assert.Equal(unknownEmailEx.Message, wrongPasswordEx.Message);
        Assert.Equal(unknownEmailEx.StatusCode, wrongPasswordEx.StatusCode);
        Assert.Equal(unknownEmailEx.Code, wrongPasswordEx.Code);
    }

    // ─── Generic 401 — Non-Active Accounts ───────────────────────────────

    [Fact]
    public async Task HandleAsync_PendingVerificationAccount_ThrowsUnauthenticatedWithGenericMessage()
    {
        // Arrange — user exists but is pending verification
        var user = new User(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            email: "pending@example.com",
            passwordHash: "hashed:CorrectPass",
            role: UserRole.Commuter,
            status: UserStatus.PendingVerification,
            displayName: "Pending User",
            languagePreference: "fil");
        _userRepository.AddUser(user);

        var request = new LoginRequest("pending@example.com", "CorrectPass");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(request));

        // Same generic message — don't reveal account exists
        Assert.Equal("Invalid credentials.", ex.Message);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_SuspendedAccount_ThrowsUnauthenticatedWithGenericMessage()
    {
        // Arrange — user exists but is suspended
        var user = new User(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            email: "suspended@example.com",
            passwordHash: "hashed:CorrectPass",
            role: UserRole.Driver,
            status: UserStatus.Suspended,
            displayName: "Suspended User",
            languagePreference: "en");
        _userRepository.AddUser(user);

        var request = new LoginRequest("suspended@example.com", "CorrectPass");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(request));

        // Same generic message — don't reveal account exists or its status
        Assert.Equal("Invalid credentials.", ex.Message);
        Assert.Equal(401, ex.StatusCode);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static User CreateActiveUser(string email, string password, UserRole role) => new(
        id: Guid.NewGuid(),
        createdAt: DateTime.UtcNow,
        updatedAt: DateTime.UtcNow,
        email: email,
        passwordHash: $"hashed:{password}",
        role: role,
        status: UserStatus.Active,
        displayName: "Test User",
        languagePreference: "fil");

    // ─── Fakes ────────────────────────────────────────────────────────────

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string password, string hash) => hash == $"hashed:{password}";
    }

    private sealed class FakeJwtService : IJwtService
    {
        public Task<string> GenerateAccessTokenAsync(User user, CancellationToken cancellationToken = default)
            => Task.FromResult("fake-access-token");

        public Task<string> GenerateRefreshTokenAsync(User user, CancellationToken cancellationToken = default)
            => Task.FromResult("fake-refresh-token");

        public Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult<ClaimsPrincipal?>(null);

        public Task<Guid?> GetUserIdFromTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(null);

        public Task<JwtValidationResult> ValidateTokenDetailedAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(JwtValidationResult.Failure("Not configured."));
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
