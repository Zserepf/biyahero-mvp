using BiyaHero.Api.Features.Common.Exceptions;

namespace BiyaHero.Api.Features.Routing.VoteRoute;

/// <summary>
/// Thrown when route voting fails input validation.
/// Maps to HTTP 422 with code "input.validation_failed".
/// </summary>
public sealed class VoteRouteValidationException : BiyaHeroException
{
    public VoteRouteValidationException(string message, object? details = null)
        : base(
            code: "input.validation_failed",
            statusCode: 422,
            message: message,
            details: details)
    {
    }
}
