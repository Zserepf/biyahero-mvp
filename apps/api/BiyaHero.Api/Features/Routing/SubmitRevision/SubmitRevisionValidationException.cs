using BiyaHero.Api.Features.Common.Exceptions;

namespace BiyaHero.Api.Features.Routing.SubmitRevision;

/// <summary>
/// Thrown when a route revision submission fails input validation.
/// Maps to HTTP 422 with code "input.validation_failed".
/// </summary>
public sealed class SubmitRevisionValidationException : BiyaHeroException
{
    public SubmitRevisionValidationException(string message, object? details = null)
        : base(
            code: "input.validation_failed",
            statusCode: 422,
            message: message,
            details: details)
    {
    }
}
