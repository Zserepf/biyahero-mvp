using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Repository interface for User-specific data access.
/// Extends the generic IRepository with email-based lookups
/// and user-management operations required by Auth_Service.
/// Requirements: 5.1, 5.5, 5.8, 5.9, 10.3
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Finds a user by their email address (case-insensitive via citext).
    /// Returns null if no user exists with the given email.
    /// </summary>
    Task<User?> FindByEmailAsync(string email);

    /// <summary>
    /// Checks whether a user with the given email already exists.
    /// Used during registration to enforce email uniqueness (Req 5.5).
    /// </summary>
    Task<bool> EmailExistsAsync(string email);

    /// <summary>
    /// Updates the language preference for a user.
    /// Only "en" and "fil" are valid values (Req 10.3).
    /// </summary>
    Task<User> UpdateLanguagePreferenceAsync(Guid userId, string languagePreference);

    /// <summary>
    /// Suspends a user account by setting status to Suspended.
    /// Used by Super Admin user-management endpoints (Req 5.8).
    /// </summary>
    Task<User> SuspendAsync(Guid userId);

    /// <summary>
    /// Changes the role of a user.
    /// Used by Super Admin for role promotion/demotion (Req 5.9).
    /// </summary>
    Task<User> ChangeRoleAsync(Guid userId, UserRole newRole);
}
