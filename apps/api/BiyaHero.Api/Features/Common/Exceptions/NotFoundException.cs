namespace BiyaHero.Api.Features.Common.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist.
/// Maps to HTTP 404 with code "resource.not_found".
/// </summary>
public sealed class NotFoundException : BiyaHeroException
{
    public NotFoundException(string message = "Resource not found.", object? details = null)
        : base("resource.not_found", 404, message, details)
    {
    }
}
