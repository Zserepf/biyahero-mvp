namespace BiyaHero.Api.Features.Auth.VerifyEmail;

/// <summary>
/// Request payload for the email verification endpoint.
/// Contains the single-use verification token sent to the user's email.
/// </summary>
public sealed record VerifyEmailRequest(string Token);
