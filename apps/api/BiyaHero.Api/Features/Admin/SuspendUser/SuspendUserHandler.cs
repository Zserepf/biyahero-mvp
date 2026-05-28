using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Admin.SuspendUser;

/// <summary>
/// Business logic for suspending a user account (POST /v1/admin/users/{id}/:suspend).
/// Authenticates the caller, verifies SuperAdmin role, then suspends the target user.
/// Requirements: 5.8
/// </summary>
public sealed class SuspendUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;

    public SuspendUserHandler(IUserRepository userRepository, IJwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Suspends a user account by setting their status to Suspended.
    /// Throws UnauthenticatedException for missing/invalid JWT (401).
    /// Throws ForbiddenException if user is not SuperAdmin (403).
    /// Throws NotFoundException if target user does not exist (404).
    /// </summary>
    public async Task<SuspendUserResponse> HandleAsync(
        string? authorizationHeader,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await AuthenticateAsync(authorizationHeader, cancellationToken);
        AuthorizeSuperAdmin(validationResult.Role);

        // Verify target user exists
        var targetUser = await _userRepository.FindByIdAsync(targetUserId);
        if (targetUser is null)
        {
            throw new NotFoundException($"User with ID '{targetUserId}' not found.");
        }

        // Suspend the user
        var suspendedUser = await _userRepository.SuspendAsync(targetUserId);

        return new SuspendUserResponse(
            UserId: suspendedUser.Id,
            Status: suspendedUser.Status.ToString(),
            Message: "User account suspended successfully.");
    }

    private async Task<JwtValidationResult> AuthenticateAsync(string? authorizationHeader, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthenticatedException("Missing or invalid Authorization header.");
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();

        var validationResult = await _jwtService.ValidateTokenDetailedAsync(token, ct);
        if (!validationResult.IsValid)
        {
            throw new UnauthenticatedException(validationResult.ErrorMessage ?? "Invalid token.");
        }

        return validationResult;
    }

    private static void AuthorizeSuperAdmin(string? role)
    {
        if (string.IsNullOrWhiteSpace(role) ||
            !string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Insufficient permissions. SuperAdmin role required.");
        }
    }
}
