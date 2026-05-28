using BiyaHero.Api.Features.Common.Exceptions;

namespace BiyaHero.Api.Features.Auth.VerifyEmail;

/// <summary>
/// Thrown when the verification request fails input validation (e.g., missing token).
/// Maps to HTTP 422 Unprocessable Entity.
/// </summary>
public sealed class ValidationException : BiyaHeroException
{
    public ValidationException(string message)
        : base(
            code: "input.validation_failed",
            statusCode: 422,
            message: message)
    {
    }
}
