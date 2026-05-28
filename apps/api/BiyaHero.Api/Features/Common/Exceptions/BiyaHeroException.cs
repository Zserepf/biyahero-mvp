namespace BiyaHero.Api.Features.Common.Exceptions;

/// <summary>
/// Base exception for all BiyaHero domain exceptions.
/// Handlers raise specific subclasses; the global middleware translates them to HTTP responses.
/// </summary>
public abstract class BiyaHeroException : Exception
{
    /// <summary>
    /// Machine-readable error code (e.g., "auth.unauthenticated", "input.validation_failed").
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// HTTP status code to return.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Optional structured details for the error response.
    /// </summary>
    public object? Details { get; }

    protected BiyaHeroException(string code, int statusCode, string message, object? details = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }
}
