namespace BiyaHero.Api.Features.Auth.Refresh;

/// <summary>
/// Request body for POST /v1/auth/sessions/:refresh (token refresh).
/// </summary>
public sealed record RefreshRequest(string RefreshToken);
