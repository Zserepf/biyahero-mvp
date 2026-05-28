using BiyaHero.Api.Features.Common.Exceptions;

namespace BiyaHero.Api.Features.Routing.CreateRoute;

/// <summary>
/// Thrown when route creation fails input validation.
/// Maps to HTTP 422 with code "input.validation_failed".
/// </summary>
public sealed class CreateRouteValidationException : BiyaHeroException
{
    public CreateRouteValidationException(string message, object? details = null)
        : base(
            code: "input.validation_failed",
            statusCode: 422,
            message: message,
            details: details)
    {
    }
}
