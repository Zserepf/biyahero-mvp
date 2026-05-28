using System.Linq.Expressions;
using System.Security.Claims;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Admin.ListUsers;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Tests.Features.Admin;

public class ListUsersHandlerTests
{
    private readonly FakeJwtService _jwtService = new();
    private readonly FakeUserRepository _userRepository = new();
    private readonly ListUsersHandler _handler;

    public ListUsersHandlerTests()
    {
        _handler = new ListUsersHandler(_userRepository, _jwtService);
    }

    [Fact]
    public async Task HandleAsync_ValidSuperAdmin_ReturnsAllUsers()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(adminId, "admin@test.com", "SuperAdmin"));

        var user1 = new User(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow,
            "user1@test.com", "hash1", UserRole.Commuter, UserStatus.Active, "User One", "en");
        var user2 = new User(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow,
            "user2@test.com", "hash2", UserRole.Driver, UserStatus.Active, "User Two", "fil");
        _userRepository.AddUser(user1);
        _userRepository.AddUser(user2);

        // Act
        var result = await _handler.HandleAsync("Bearer valid-token");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, u => u.Email == "user1@test.com");
        Assert.Contains(result, u => u.Email == "user2@test.com");
    }

    [Fact]
    public async Task HandleAsync_MissingAuthHeader_ThrowsUnauthenticatedException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(null));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidToken_ThrowsUnauthenticatedException()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Failure("Token expired."));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync("Bearer expired-token"));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_NonSuperAdminRole_ThrowsForbiddenException()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Success(Guid.NewGuid(), "mod@test.com", "Moderator"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.HandleAsync("Bearer valid-token"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_CommuterRole_ThrowsForbiddenException()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Success(Guid.NewGuid(), "user@test.com", "Commuter"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.HandleAsync("Bearer valid-token"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_EmptyUserList_ReturnsEmptyList()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Success(Guid.NewGuid(), "admin@test.com", "SuperAdmin"));

        // Act
        var result = await _handler.HandleAsync("Bearer valid-token");

        // Assert
        Assert.Empty(result);
    }

    // ─── Fakes ──────────────────────────────────────────────────────────

    private sealed class FakeJwtService : IJwtService
    {
        private JwtValidationResult _validationResult = JwtValidationResult.Failure("Not configured.");

        public void SetValidationResult(JwtValidationResult result) => _validationResult = result;

        public Task<string> GenerateAccessTokenAsync(User user, CancellationToken ct = default)
            => Task.FromResult("fake-access-token");

        public Task<string> GenerateRefreshTokenAsync(User user, CancellationToken ct = default)
            => Task.FromResult("fake-refresh-token");

        public Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken ct = default)
            => Task.FromResult<ClaimsPrincipal?>(null);

        public Task<Guid?> GetUserIdFromTokenAsync(string token, CancellationToken ct = default)
            => Task.FromResult(_validationResult.UserId);

        public Task<JwtValidationResult> ValidateTokenDetailedAsync(string token, CancellationToken ct = default)
            => Task.FromResult(_validationResult);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, User> _users = new();

        public void AddUser(User user) => _users[user.Id] = user;

        public Task<User?> FindByIdAsync(Guid id)
            => Task.FromResult(_users.GetValueOrDefault(id));

        public Task<IReadOnlyList<User>> FindAllAsync()
            => Task.FromResult<IReadOnlyList<User>>(_users.Values.ToList().AsReadOnly());

        public Task<User> CreateAsync(User entity) { _users[entity.Id] = entity; return Task.FromResult(entity); }
        public Task<IReadOnlyList<User>> WhereAsync(Expression<Func<User, bool>> predicate)
            => Task.FromResult<IReadOnlyList<User>>(_users.Values.Where(predicate.Compile()).ToList().AsReadOnly());
        public Task<User> SaveAsync(User entity) { _users[entity.Id] = entity; return Task.FromResult(entity); }
        public Task<User> UpdateAsync(User entity) { _users[entity.Id] = entity; return Task.FromResult(entity); }
        public Task DeleteAsync(User entity) { _users.Remove(entity.Id); return Task.CompletedTask; }
        public Task<User?> FindByEmailAsync(string email)
            => Task.FromResult(_users.Values.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
        public Task<bool> EmailExistsAsync(string email)
            => Task.FromResult(_users.Values.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
        public Task<User> UpdateLanguagePreferenceAsync(Guid userId, string languagePreference)
        {
            var user = _users[userId];
            user.LanguagePreference = languagePreference;
            return Task.FromResult(user);
        }
        public Task<User> SuspendAsync(Guid userId)
        {
            var user = _users[userId];
            user.Status = UserStatus.Suspended;
            return Task.FromResult(user);
        }
        public Task<User> ChangeRoleAsync(Guid userId, UserRole newRole)
        {
            var user = _users[userId];
            user.Role = newRole;
            return Task.FromResult(user);
        }
    }
}
