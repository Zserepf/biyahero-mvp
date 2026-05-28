namespace BiyaHero.Api.Features.Auth.VerifyEmail;

/// <summary>
/// Response payload for a successful email verification.
/// Returns a confirmation message and the verified email address.
/// </summary>
public sealed record VerifyEmailResponse(string Message, string Email);
