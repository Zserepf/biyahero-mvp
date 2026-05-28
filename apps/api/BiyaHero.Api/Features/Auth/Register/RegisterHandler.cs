using System.Security.Cryptography;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Auth.VerifyEmail;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Auth.Register;

/// <summary>
/// Business logic handler for user registration.
/// Validates input, checks email uniqueness, hashes password, persists user,
/// generates a verification token, and sends a verification email via SES.
/// Requirements: 5.1, 5.5
/// </summary>
public sealed class RegisterHandler
{
    private static readonly TimeSpan VerificationTokenExpiry = TimeSpan.FromHours(24);
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Commuter",
        "Driver"
    };

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IVerificationTokenStore _tokenStore;
    private readonly IEmailService _emailService;

    public RegisterHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IVerificationTokenStore tokenStore,
        IEmailService emailService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenStore = tokenStore;
        _emailService = emailService;
    }

    /// <summary>
    /// Processes a registration request.
    /// </summary>
    /// <exception cref="RegisterValidationException">Thrown when input is invalid.</exception>
    /// <exception cref="EmailConflictException">Thrown when email already exists (opaque message).</exception>
    public async Task<RegisterResponse> HandleAsync(RegisterRequest request)
    {
        ValidateRequest(request);

        // Check email uniqueness — opaque 409 regardless of verification status (Req 5.5)
        var emailExists = await _userRepository.EmailExistsAsync(request.Email);
        if (emailExists)
        {
            throw new EmailConflictException();
        }

        // Hash password with Argon2id
        var passwordHash = _passwordHasher.Hash(request.Password);

        // Create user — Active immediately in Development so login works without email verification.
        // In Production, set Status = PendingVerification and require email confirmation.
        var role = Enum.Parse<UserRole>(request.Role, ignoreCase: true);
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        var initialStatus = isDevelopment ? UserStatus.Active : UserStatus.PendingVerification;

        var user = new User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = role,
            Status = initialStatus,
            DisplayName = request.DisplayName.Trim()
        };

        await _userRepository.CreateAsync(user);

        if (!isDevelopment)
        {
            // Generate a URL-safe verification token and send email (Production only)
            var verificationToken = GenerateVerificationToken();
            await _tokenStore.StoreTokenAsync(user.Id, verificationToken, VerificationTokenExpiry);
            await _emailService.SendVerificationEmailAsync(user.Email, verificationToken);
        }

        var message = isDevelopment
            ? "Account created successfully. You can now log in."
            : "Registration successful. Please check your email to verify your account.";

        return new RegisterResponse(
            UserId: user.Id,
            Email: user.Email,
            Message: message
        );
    }

    private static void ValidateRequest(RegisterRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Email))
            errors.Add("Email is required.");

        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
            errors.Add("Email format is invalid.");

        if (string.IsNullOrWhiteSpace(request.Password))
            errors.Add("Password is required.");

        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < 8)
            errors.Add("Password must be at least 8 characters.");

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            errors.Add("Display name is required.");

        if (string.IsNullOrWhiteSpace(request.Role))
            errors.Add("Role is required.");

        if (!string.IsNullOrWhiteSpace(request.Role) && !AllowedRoles.Contains(request.Role))
            errors.Add("Role must be 'Commuter' or 'Driver'.");

        if (errors.Count > 0)
        {
            throw new RegisterValidationException(
                "Registration validation failed.",
                new { errors });
        }
    }

    /// <summary>
    /// Generates a cryptographically random URL-safe token string.
    /// </summary>
    private static string GenerateVerificationToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
