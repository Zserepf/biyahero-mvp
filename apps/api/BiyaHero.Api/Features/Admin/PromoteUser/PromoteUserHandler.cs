using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Admin.PromoteUser;

/// <summary>
/// Business logic for promoting/changing a user's role (POST /v1/admin/users/{id}/:promote).
/// Authenticates the caller, verifies SuperAdmin role, requires password re-entry as 2FA
/// confirmation before applying the role change.
/// Requirements: 5.9, 5.11
/// </summary>
public sealed class PromoteUserHandler
{
    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(UserRole.Commuter),
        nameof(UserRole.Driver),
        nameof(UserRole.Moderator),
        nameof(UserRole.SuperAdmin)
    };

    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IPasswordHasher _passwordHasher;

    public PromoteUserHandler(
        IUserRepository userRepository,
        IJwtService jwtService,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// Changes a user's role after verifying the acting admin's password as 2FA.
    /// Throws UnauthenticatedException for missing/invalid JWT (401).
    /// Throws ForbiddenException if user is not SuperAdmin (403).
    /// Throws NotFoundException if target user does not exist (404).
    /// Throws ValidationFailedException for invalid role or missing password (422).
    /// </summary>
    public async Task<PromoteUserResponse> HandleAsync(
        string? authorizationHeader,
        Guid targetUserId,
        PromoteUserRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Authenticate — extract user identity and role from JWT
        var validationResult = await AuthenticateAsync(authorizationHeader, cancellationToken);

        // 2. Authorize — only SuperAdmin can promote users
        AuthorizeSuperAdmin(validationResult.Role);

        // 3. Validate request body
        ValidateRequest(request);

        // 4. Verify the acting admin's password as 2FA confirmation (Req 5.11)
        var actingAdmin = await _userRepository.FindByIdAsync(validationResult.UserId!.Value);
        if (actingAdmin is null)
        {
            throw new UnauthenticatedException("Acting admin account not found.");
        }

        if (!_passwordHasher.Verify(request.Password, actingAdmin.PasswordHash))
        {
            throw new ForbiddenException("Password verification failed. 2FA confirmation denied.");
        }

        // 5. Verify target user exists
        var targetUser = await _userRepository.FindByIdAsync(targetUserId);
        if (targetUser is null)
        {
            throw new NotFoundException($"User with ID '{targetUserId}' not found.");
        }

        // 6. Parse and apply the new role
        var newRole = Enum.Parse<UserRole>(request.NewRole, ignoreCase: true);
        var updatedUser = await _userRepository.ChangeRoleAsync(targetUserId, newRole);

        return new PromoteUserResponse(
            UserId: updatedUser.Id,
            NewRole: updatedUser.Role.ToString(),
            Message: $"User role changed to {updatedUser.Role} successfully.");
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

    private static void ValidateRequest(PromoteUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationFailedException("Password is required for 2FA confirmation.");
        }

        if (string.IsNullOrWhiteSpace(request.NewRole))
        {
            throw new ValidationFailedException("NewRole is required.");
        }

        if (!ValidRoles.Contains(request.NewRole))
        {
            throw new ValidationFailedException(
                $"Invalid role '{request.NewRole}'. Valid roles: {string.Join(", ", ValidRoles)}.");
        }
    }
}
