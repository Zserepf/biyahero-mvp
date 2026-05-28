namespace BiyaHero.Api.Features.Common.Exceptions;

/// <summary>
/// Thrown when an operation conflicts with the current state of a resource.
/// Maps to HTTP 409 with code "resource.conflict".
/// </summary>
public sealed class ConflictException : BiyaHeroException
{
    public ConflictException(string message = "Resource conflict.", object? details = null)
        : base("resource.conflict", 409, message, details)
    {
    }
}
