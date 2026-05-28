namespace BiyaHero.Api.Features.Auth.Login;

/// <summary>
/// Request body for POST /v1/auth/sessions (login).
/// </summary>
public sealed record LoginRequest(string Email, string Password);
