using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Admin.ListUsers;

/// <summary>
/// Business logic for listing all users (GET /v1/admin/users).
/// Authenticates the caller and verifies SuperAdmin role before returning the user list.
/// Requirements: 5.8, 5.9
/// </summary>
public sealed class ListUsersHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;

    public ListUsersHandler(IUserRepository userRepository, IJwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Returns all users for Super Admin management.
    /// Throws UnauthenticatedException for missing/invalid JWT (401).
    /// Throws ForbiddenException if user is not SuperAdmin (403).
    /// </summary>
    public async Task<IReadOnlyList<ListUsersItemDto>> HandleAsync(
        string? authorizationHeader,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await AuthenticateAsync(authorizationHeader, cancellationToken);
        AuthorizeSuperAdmin(validationResult.Role);

        var users = await _userRepository.FindAllAsync();

        return users.Select(u => new ListUsersItemDto(
            Id: u.Id,
            Email: u.Email,
            Role: u.Role.ToString(),
            Status: u.Status.ToString(),
            DisplayName: u.DisplayName,
            LanguagePreference: u.LanguagePreference,
            CreatedAt: u.CreatedAt,
            UpdatedAt: u.UpdatedAt
        )).ToList();
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
