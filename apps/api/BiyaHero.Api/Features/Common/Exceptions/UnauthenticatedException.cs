namespace BiyaHero.Api.Features.Common.Exceptions;

/// <summary>
/// Thrown when a request lacks valid authentication credentials.
/// Maps to HTTP 401 with code "auth.unauthenticated".
/// </summary>
public sealed class UnauthenticatedException : BiyaHeroException
{
    public UnauthenticatedException(string message = "Authentication required.", object? details = null)
        : base("auth.unauthenticated", 401, message, details)
    {
    }
}
