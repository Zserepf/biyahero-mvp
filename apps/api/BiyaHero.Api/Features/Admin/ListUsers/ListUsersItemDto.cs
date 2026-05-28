namespace BiyaHero.Api.Features.Admin.ListUsers;

/// <summary>
/// DTO representing a single user in the admin user list response.
/// </summary>
public sealed record ListUsersItemDto(
    Guid Id,
    string Email,
    string Role,
    string Status,
    string DisplayName,
    string LanguagePreference,
    DateTime CreatedAt,
    DateTime UpdatedAt);
