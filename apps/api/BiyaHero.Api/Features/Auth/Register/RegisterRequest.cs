namespace BiyaHero.Api.Features.Auth.Register;

/// <summary>
/// Request payload for user registration: POST /v1/auth/registrations.
/// Role is constrained to Commuter or Driver at registration time.
/// </summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    string Role);
