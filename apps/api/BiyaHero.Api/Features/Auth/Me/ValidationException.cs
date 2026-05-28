using BiyaHero.Api.Features.Common.Exceptions;

namespace BiyaHero.Api.Features.Auth.Me;

/// <summary>
/// Thrown when the language preference update fails input validation.
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
