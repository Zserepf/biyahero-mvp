namespace BiyaHero.Api.Domain;

/// <summary>
/// Account lifecycle statuses for a User.
/// Maps to the PostgreSQL user_status enum.
/// </summary>
public enum UserStatus
{
    PendingVerification,
    Active,
    Suspended
}
