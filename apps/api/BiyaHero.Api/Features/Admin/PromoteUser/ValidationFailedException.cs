using BiyaHero.Api.Features.Common.Exceptions;

namespace BiyaHero.Api.Features.Admin.PromoteUser;

/// <summary>
/// Thrown when the promote user request fails input validation.
/// Maps to HTTP 422 with code "input.validation_failed".
/// </summary>
public sealed class ValidationFailedException : BiyaHeroException
{
    public ValidationFailedException(string message, object? details = null)
        : base(
            code: "input.validation_failed",
            statusCode: 422,
            message: message,
            details: details)
    {
    }
}
