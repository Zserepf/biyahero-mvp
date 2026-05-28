using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Auth.Login;

/// <summary>
/// Business logic for user login (POST /v1/auth/sessions).
/// Validates credentials, checks account status, and issues JWT tokens.
/// Returns a generic 401 for ALL failure cases (unknown email, wrong password,
/// pending_verification, suspended) to prevent information leakage about account existence.
/// Requirements: 5.3, 5.6
/// </summary>
public sealed class LoginHandler
{
    private const int AccessTokenExpiresInSeconds = 86400; // 24 hours
    private const string InvalidCredentialsMessage = "Invalid credentials.";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public LoginHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Authenticates a user and returns tokens + user info.
    /// Throws UnauthenticatedException with a generic message for ALL failure scenarios:
    /// - Unknown email (Req 5.6: no info leak about which field failed)
    /// - Wrong password (Req 5.6: same generic message)
    /// - Account pending verification (don't reveal account exists)
    /// - Account suspended (don't reveal account exists)
    /// </summary>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Look up user by email (case-insensitive via citext column)
        var user = await _userRepository.FindByEmailAsync(request.Email);

        // If user not found — generic 401
        if (user is null)
        {
            throw new UnauthenticatedException(InvalidCredentialsMessage);
        }

        // If password doesn't match — generic 401
        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthenticatedException(InvalidCredentialsMessage);
        }

        // If account is not active (pending_verification or suspended) — generic 401
        // Per Req 5.6: don't reveal that the account exists by returning a different status code
        if (user.Status != UserStatus.Active)
        {
            throw new UnauthenticatedException(InvalidCredentialsMessage);
        }

        // Issue JWT access token (24h) + refresh token (30d)
        var accessToken = await _jwtService.GenerateAccessTokenAsync(user, cancellationToken);
        var refreshToken = await _jwtService.GenerateRefreshTokenAsync(user, cancellationToken);

        var userDto = new LoginUserDto(
            Id: user.Id,
            Email: user.Email,
            Role: user.Role.ToString(),
            DisplayName: user.DisplayName,
            LanguagePreference: user.LanguagePreference);

        return new LoginResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresIn: AccessTokenExpiresInSeconds,
            User: userDto);
    }
}
