namespace BiyaHero.Api.Features.Auth.VerifyEmail;

/// <summary>
/// Maps the email verification endpoint: POST /v1/auth/email-verifications/:verify
/// Custom method per REST API steering (colon syntax, always POST).
/// No authentication required — the token itself is the proof of ownership.
/// Requirements: 5.2
/// </summary>
public static class VerifyEmailEndpoint
{
    public static void MapVerifyEmailEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/auth/email-verifications/:verify", async (
            VerifyEmailRequest request,
            VerifyEmailHandler handler) =>
        {
            var response = await handler.HandleAsync(request);
            return Results.Ok(response);
        })
        .WithName("VerifyEmail")
        .WithTags("Auth")
        .AllowAnonymous();
    }
}
