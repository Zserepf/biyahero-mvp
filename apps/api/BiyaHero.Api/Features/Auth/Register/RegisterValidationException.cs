using BiyaHero.Api.Features.Common.Exceptions;

namespace BiyaHero.Api.Features.Auth.Register;

/// <summary>
/// Thrown when registration input fails validation (missing fields, invalid role, etc.).
/// Maps to HTTP 422 with code "input.validation_failed".
/// </summary>
public sealed class RegisterValidationException : BiyaHeroException
{
    public RegisterValidationException(string message, object? details = null)
        : base("input.validation_failed", 422, message, details)
    {
    }
}
