namespace BiyaHero.Api.Features.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated user lacks the required role or permission.
/// Maps to HTTP 403 with code "auth.forbidden".
/// </summary>
public sealed class ForbiddenException : BiyaHeroException
{
    public ForbiddenException(string message = "Insufficient permissions.", object? details = null)
        : base("auth.forbidden", 403, message, details)
    {
    }
}
