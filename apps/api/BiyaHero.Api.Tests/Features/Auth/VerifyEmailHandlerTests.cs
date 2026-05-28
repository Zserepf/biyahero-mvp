using System.Linq.Expressions;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Auth.VerifyEmail;
using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.Tests.Features.Auth;

public class VerifyEmailHandlerTests
{
    private readonly InMemoryVerificationTokenStore _tokenStore = new();
    private readonly FakeUserRepository _userRepository = new();
    private readonly VerifyEmailHandler _handler;

    public VerifyEmailHandlerTests()
    {
        _handler = new VerifyEmailHandler(_tokenStore, _userRepository);
    }

    [Fact]
    public async Task HandleAsync_ValidToken_ActivatesUserAndReturnsSuccess()
    {
        // Arrange
        var user = CreatePendingUser();
        _userRepository.AddUser(user);
        var token = "valid-token-123";
        await _tokenStore.StoreTokenAsync(user.Id, token, TimeSpan.FromHours(24));

        // Act
        var response = await _handler.HandleAsync(new VerifyEmailRequest(token));

        // Assert
        Assert.Equal("Email verified successfully.", response.Message);
        Assert.Equal(user.Email, response.Email);

        var updatedUser = await _userRepository.FindByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserStatus.Active, updatedUser.Status);
    }

    [Fact]
    public async Task HandleAsync_InvalidToken_ThrowsInvalidTokenException()
    {
        // Arrange
        var request = new VerifyEmailRequest("nonexistent-token");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidTokenException>(
            () => _handler.HandleAsync(request));
    }

    [Fact]
    public async Task HandleAsync_ExpiredToken_ThrowsInvalidTokenException()
    {
        // Arrange
        var user = CreatePendingUser();
        _userRepository.AddUser(user);
        var token = "expired-token";
        // Store with zero expiry so it's immediately expired
        await _tokenStore.StoreTokenAsync(user.Id, token, TimeSpan.FromMilliseconds(-1));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidTokenException>(
            () => _handler.HandleAsync(new VerifyEmailRequest(token)));
    }

    [Fact]
    public async Task HandleAsync_TokenConsumedOnce_SecondAttemptThrows()
    {
        // Arrange
        var user = CreatePendingUser();
        _userRepository.AddUser(user);
        var token = "single-use-token";
        await _tokenStore.StoreTokenAsync(user.Id, token, TimeSpan.FromHours(24));

        // Act — first use succeeds
        await _handler.HandleAsync(new VerifyEmailRequest(token));

        // Assert — second use fails (token consumed)
        await Assert.ThrowsAsync<InvalidTokenException>(
            () => _handler.HandleAsync(new VerifyEmailRequest(token)));
    }

    [Fact]
    public async Task HandleAsync_EmptyToken_ThrowsValidationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.HandleAsync(new VerifyEmailRequest("")));
    }

    [Fact]
    public async Task HandleAsync_WhitespaceToken_ThrowsValidationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.HandleAsync(new VerifyEmailRequest("   ")));
    }

    [Fact]
    public async Task HandleAsync_ValidToken_UserNotFound_ThrowsInvalidTokenException()
    {
        // Arrange — token points to a user ID that doesn't exist in the repo
        var missingUserId = Guid.NewGuid();
        var token = "orphan-token";
        await _tokenStore.StoreTokenAsync(missingUserId, token, TimeSpan.FromHours(24));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidTokenException>(
            () => _handler.HandleAsync(new VerifyEmailRequest(token)));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static User CreatePendingUser() => new(
        id: Guid.NewGuid(),
        createdAt: DateTime.UtcNow,
        updatedAt: DateTime.UtcNow,
        email: "commuter@biyahero.ph",
        passwordHash: "hashed-password",
        role: UserRole.Commuter,
        status: UserStatus.PendingVerification,
        displayName: "Test Commuter",
        languagePreference: "fil");

    /// <summary>
    /// Minimal in-memory user repository for testing the handler in isolation.
    /// </summary>
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
            user.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(user);
        }

        public Task<User> SuspendAsync(Guid userId)
        {
            if (!_users.TryGetValue(userId, out var user))
                throw new InvalidOperationException($"User with id {userId} not found.");
            user.Status = UserStatus.Suspended;
            user.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(user);
        }

        public Task<User> ChangeRoleAsync(Guid userId, UserRole newRole)
        {
            if (!_users.TryGetValue(userId, out var user))
                throw new InvalidOperationException($"User with id {userId} not found.");
            user.Role = newRole;
            user.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(user);
        }
    }
}
