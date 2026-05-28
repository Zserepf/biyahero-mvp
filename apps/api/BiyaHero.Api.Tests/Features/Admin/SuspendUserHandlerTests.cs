using System.Linq.Expressions;
using System.Security.Claims;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Admin.SuspendUser;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Tests.Features.Admin;

public class SuspendUserHandlerTests
{
    private readonly FakeJwtService _jwtService = new();
    private readonly FakeUserRepository _userRepository = new();
    private readonly SuspendUserHandler _handler;

    public SuspendUserHandlerTests()
    {
        _handler = new SuspendUserHandler(_userRepository, _jwtService);
    }

    [Fact]
    public async Task HandleAsync_ValidSuperAdmin_SuspendsUser()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        _jwtService.SetValidationResult(JwtValidationResult.Success(adminId, "admin@test.com", "SuperAdmin"));

        var targetUser = new User(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow,
            "target@test.com", "hash", UserRole.Commuter, UserStatus.Active, "Target User", "en");
        _userRepository.AddUser(targetUser);

        // Act
        var result = await _handler.HandleAsync("Bearer valid-token", targetUser.Id);

        // Assert
        Assert.Equal(targetUser.Id, result.UserId);
        Assert.Equal("Suspended", result.Status);
    }

    [Fact]
    public async Task HandleAsync_MissingAuthHeader_ThrowsUnauthenticatedException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync(null, Guid.NewGuid()));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidToken_ThrowsUnauthenticatedException()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Failure("Token expired."));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => _handler.HandleAsync("Bearer expired-token", Guid.NewGuid()));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_NonSuperAdminRole_ThrowsForbiddenException()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Success(Guid.NewGuid(), "mod@test.com", "Moderator"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.HandleAsync("Bearer valid-token", Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_TargetUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Success(Guid.NewGuid(), "admin@test.com", "SuperAdmin"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.HandleAsync("Bearer valid-token", Guid.NewGuid()));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_DriverRole_ThrowsForbiddenException()
    {
        // Arrange
        _jwtService.SetValidationResult(JwtValidationResult.Success(Guid.NewGuid(), "driver@test.com", "Driver"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.HandleAsync("Bearer valid-token", Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
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
