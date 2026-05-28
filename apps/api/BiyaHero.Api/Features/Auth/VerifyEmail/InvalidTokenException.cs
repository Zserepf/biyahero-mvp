using BiyaHero.Api.Features.Common.Exceptions;

namespace BiyaHero.Api.Features.Auth.VerifyEmail;

/// <summary>
/// Thrown when a verification token is invalid, expired, or already consumed.
/// Maps to HTTP 400 Bad Request per the design (invalid/expired token → 400).
/// </summary>
public sealed class InvalidTokenException : BiyaHeroException
{
    public InvalidTokenException()
        : base(
            code: "auth.invalid_token",
            statusCode: 400,
            message: "The verification token is invalid or has expired.")
    {
    }
}
