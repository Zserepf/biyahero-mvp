using BiyaHero.Api.Features.Common.Exceptions;

namespace BiyaHero.Api.Features.Auth.Register;

/// <summary>
/// Thrown when a registration attempt uses an email that already has an account.
/// Maps to HTTP 409 with code "auth.email_conflict".
/// The message is intentionally opaque — it does not reveal whether the existing
/// account is verified or not (Req 5.5).
/// </summary>
public sealed class EmailConflictException : BiyaHeroException
{
    public EmailConflictException()
        : base("auth.email_conflict", 409, "An account with this email already exists.")
    {
    }
}
