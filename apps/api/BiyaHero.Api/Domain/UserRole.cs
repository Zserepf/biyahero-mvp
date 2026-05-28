namespace BiyaHero.Api.Domain;

/// <summary>
/// Roles available to users in the BiyaHero platform.
/// Maps to the PostgreSQL user_role enum.
/// </summary>
public enum UserRole
{
    Commuter,
    Driver,
    Moderator,
    SuperAdmin
}
