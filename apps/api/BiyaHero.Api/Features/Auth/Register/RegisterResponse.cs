namespace BiyaHero.Api.Features.Auth.Register;

/// <summary>
/// Response payload for a successful registration.
/// Returns the new user's ID, email, and a confirmation message.
/// </summary>
public sealed record RegisterResponse(
    Guid UserId,
    string Email,
    string Message);
