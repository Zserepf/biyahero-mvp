using System.Linq.Expressions;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Auth.Register;
using BiyaHero.Api.Features.Auth.VerifyEmail;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Tests.Features.Auth;

/// <summary>
/// Unit tests for RegisterHandler.
/// Validates: Requirements 5.1, 5.5
/// </summary>
public class RegisterHandlerTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly InMemoryVerificationTokenStore _tokenStore = new();
    private readonly FakeEmailService _emailService = new();
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        _handler = new RegisterHandler(
            _userRepository,
            _passwordHasher,
            _tokenStore,
            _emailService);
    }

    // ─── Successful Registration ──────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidRequest_ReturnsCreatedResponse()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: "commuter@example.com",
            Password: "SecurePass123",
            DisplayName: "Juan Dela Cruz",
            Role: "Commuter");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        Assert.NotEqual(Guid.Empty, response.UserId);
        Assert.Equal("commuter@example.com", response.Email);
        Assert.Contains("verify", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_CreatesUserWithPendingVerificationStatus()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: "driver@example.com",
            Password: "SecurePass123",
            DisplayName: "Maria Santos",
            Role: "Driver");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        var user = await _userRepository.FindByIdAsync(response.UserId);
        Assert.NotNull(user);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_HashesPasswordWithArgon2id()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "MyPassword123",
            DisplayName: "Test User",
            Role: "Commuter");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        var user = await _userRepository.FindByIdAsync(response.UserId);
        Assert.NotNull(user);
        Assert.NotEqual("MyPassword123", user.PasswordHash);
        Assert.Equal("hashed:MyPassword123", user.PasswordHash); // Fake hasher prefix
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_SendsVerificationEmail()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: "verify@example.com",
            Password: "SecurePass123",
            DisplayName: "Verify User",
            Role: "Commuter");

        // Act
        await _handler.HandleAsync(request);

        // Assert
        Assert.Single(_emailService.SentEmails);
        Assert.Equal("verify@example.com", _emailService.SentEmails[0].ToEmail);
        Assert.NotEmpty(_emailService.SentEmails[0].Token);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_ResponseDoesNotLeakPasswordOrToken()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: "noleak@example.com",
            Password: "SecretPassword",
            DisplayName: "No Leak",
            Role: "Driver");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert — response only contains userId, email, and message
        Assert.DoesNotContain("SecretPassword", response.Message);
        Assert.DoesNotContain("SecretPassword", response.Email);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_NormalizesEmailToLowercase()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: "  User@EXAMPLE.COM  ",
            Password: "SecurePass123",
            DisplayName: "Case Test",
            Role: "Commuter");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        var user = await _userRepository.FindByIdAsync(response.UserId);
        Assert.NotNull(user);
        Assert.Equal("user@example.com", user.Email);
    }

    [Fact]
    public async Task HandleAsync_DriverRole_CreatesUserWithDriverRole()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: "driver@example.com",
            Password: "SecurePass123",
            DisplayName: "Driver User",
            Role: "Driver");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        var user = await _userRepository.FindByIdAsync(response.UserId);
        Assert.NotNull(user);
        Assert.Equal(UserRole.Driver, user.Role);
    }

    // ─── Email Conflict (Opaque 409) ──────────────────────────────────────

    [Fact]
    public async Task HandleAsync_EmailAlreadyExists_ThrowsEmailConflictException()
    {
        // Arrange — pre-existing user with same email
        var existingUser = new User(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            email: "taken@example.com",
            passwordHash: "hashed",
            role: UserRole.Commuter,
            status: UserStatus.Active,
            displayName: "Existing",
            languagePreference: "fil");
        _userRepository.AddUser(existingUser);

        var request = new RegisterRequest(
            Email: "taken@example.com",
            Password: "SecurePass123",
            DisplayName: "New User",
            Role: "Commuter");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<EmailConflictException>(
            () => _handler.HandleAsync(request));

        // Verify opaque message — does not reveal verification status (Req 5.5)
        Assert.DoesNotContain("verified", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("auth.email_conflict", ex.Code);
    }

    [Fact]
    public async Task HandleAsync_EmailExistsWithPendingStatus_ThrowsSameOpaqueConflict()
    {
        // Arrange — user exists but is still pending verification
        var pendingUser = new User(
            id: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            email: "pending@example.com",
            passwordHash: "hashed",
            role: UserRole.Commuter,
            status: UserStatus.PendingVerification,
            displayName: "Pending",
            languagePreference: "fil");
        _userRepository.AddUser(pendingUser);

        var request = new RegisterRequest(
            Email: "pending@example.com",
            Password: "SecurePass123",
            DisplayName: "Another User",
            Role: "Commuter");

        // Act & Assert — same opaque 409 regardless of existing account status
        var ex = await Assert.ThrowsAsync<EmailConflictException>(
            () => _handler.HandleAsync(request));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("auth.email_conflict", ex.Code);
    }

    // ─── Input Validation (422) ───────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_EmptyEmail_ThrowsValidationException()
    {
        var request = new RegisterRequest("", "SecurePass123", "Name", "Commuter");

        var ex = await Assert.ThrowsAsync<RegisterValidationException>(
            () => _handler.HandleAsync(request));

        Assert.Equal(422, ex.StatusCode);
        Assert.Equal("input.validation_failed", ex.Code);
    }

    [Fact]
    public async Task HandleAsync_InvalidEmailFormat_ThrowsValidationException()
    {
        var request = new RegisterRequest("not-an-email", "SecurePass123", "Name", "Commuter");

        await Assert.ThrowsAsync<RegisterValidationException>(
            () => _handler.HandleAsync(request));
    }

    [Fact]
    public async Task HandleAsync_EmptyPassword_ThrowsValidationException()
    {
        var request = new RegisterRequest("user@example.com", "", "Name", "Commuter");

        await Assert.ThrowsAsync<RegisterValidationException>(
            () => _handler.HandleAsync(request));
    }

    [Fact]
    public async Task HandleAsync_ShortPassword_ThrowsValidationException()
    {
        var request = new RegisterRequest("user@example.com", "short", "Name", "Commuter");

        await Assert.ThrowsAsync<RegisterValidationException>(
            () => _handler.HandleAsync(request));
    }

    [Fact]
    public async Task HandleAsync_EmptyDisplayName_ThrowsValidationException()
    {
        var request = new RegisterRequest("user@example.com", "SecurePass123", "", "Commuter");

        await Assert.ThrowsAsync<RegisterValidationException>(
            () => _handler.HandleAsync(request));
    }

    [Fact]
    public async Task HandleAsync_EmptyRole_ThrowsValidationException()
    {
        var request = new RegisterRequest("user@example.com", "SecurePass123", "Name", "");

        await Assert.ThrowsAsync<RegisterValidationException>(
            () => _handler.HandleAsync(request));
    }

    [Fact]
    public async Task HandleAsync_InvalidRole_ThrowsValidationException()
    {
        var request = new RegisterRequest("user@example.com", "SecurePass123", "Name", "SuperAdmin");

        await Assert.ThrowsAsync<RegisterValidationException>(
            () => _handler.HandleAsync(request));
    }

    [Fact]
    public async Task HandleAsync_RoleIsCaseInsensitive_AcceptsLowercase()
    {
        var request = new RegisterRequest("case@example.com", "SecurePass123", "Name", "commuter");

        var response = await _handler.HandleAsync(request);

        var user = await _userRepository.FindByIdAsync(response.UserId);
        Assert.NotNull(user);
        Assert.Equal(UserRole.Commuter, user.Role);
    }

    // ─── Fakes ────────────────────────────────────────────────────────────

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string password, string hash) => hash == $"hashed:{password}";
    }

    private sealed class FakeEmailService : IEmailService
    {
        public List<(string ToEmail, string Token)> SentEmails { get; } = new();

        public Task SendVerificationEmailAsync(string toEmail, string verificationToken)
        {
            SentEmails.Add((toEmail, verificationToken));
            return Task.CompletedTask;
        }
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
