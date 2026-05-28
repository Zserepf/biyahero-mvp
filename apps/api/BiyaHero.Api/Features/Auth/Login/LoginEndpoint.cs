namespace BiyaHero.Api.Features.Auth.Login;

/// <summary>
/// Maps POST /v1/auth/sessions to the LoginHandler.
/// Thin endpoint — delegates all business logic to the handler.
/// </summary>
public static class LoginEndpoint
{
    public static void MapLoginEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/auth/sessions", async (LoginRequest request, LoginHandler handler, CancellationToken ct) =>
        {
            var response = await handler.HandleAsync(request, ct);
            return Results.Ok(response);
        })
        .WithName("Login")
        .WithTags("Auth")
        .AllowAnonymous();
    }
}
