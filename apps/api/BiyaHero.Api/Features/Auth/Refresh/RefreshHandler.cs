using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Auth.Refresh;

/// <summary>
/// Business logic for token refresh (POST /v1/auth/sessions/:refresh).
/// Validates the refresh token, looks up the user, and issues rotated tokens.
/// Requirements: 5.3
/// </summary>
public sealed class RefreshHandler
{
    private const int AccessTokenExpiresInSeconds = 86400; // 24 hours

    private readonly IJwtService _jwtService;
    private readonly IUserRepository _userRepository;

    public RefreshHandler(IJwtService jwtService, IUserRepository userRepository)
    {
        _jwtService = jwtService;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Validates the refresh token, checks user status, and issues new rotated tokens.
    /// Throws UnauthenticatedException for invalid/expired tokens or missing users.
    /// Throws ForbiddenException for suspended users.
    /// </summary>
    public async Task<RefreshResponse> HandleAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        // Validate the refresh token
        var validationResult = await _jwtService.ValidateTokenDetailedAsync(request.RefreshToken, cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new UnauthenticatedException("Invalid or expired refresh token.");
        }

        // Verify this is a refresh token (has type="refresh" claim)
        if (validationResult.TokenType != "refresh")
        {
            throw new UnauthenticatedException("Invalid token type.");
        }

        // Look up the user by the sub claim
        var userId = validationResult.UserId!.Value;
        var user = await _userRepository.FindByIdAsync(userId);

        if (user is null)
        {
            throw new UnauthenticatedException("User not found.");
        }

        // Check account status — 403 for suspended users
        if (user.Status == UserStatus.Suspended)
        {
            throw new ForbiddenException("Account has been suspended.");
        }

        // Issue new rotated tokens
        var accessToken = await _jwtService.GenerateAccessTokenAsync(user, cancellationToken);
        var refreshToken = await _jwtService.GenerateRefreshTokenAsync(user, cancellationToken);

        return new RefreshResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresIn: AccessTokenExpiresInSeconds);
    }
}
