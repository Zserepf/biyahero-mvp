namespace BiyaHero.Api.Features.Auth.Logout;

/// <summary>
/// Maps DELETE /v1/auth/sessions/{id} to the LogoutHandler.
/// Thin endpoint — delegates all business logic to the handler.
/// </summary>
public static class LogoutEndpoint
{
    public static void MapLogoutEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/v1/auth/sessions/{id}", async (string id, HttpContext httpContext, LogoutHandler handler, CancellationToken ct) =>
        {
            var authorizationHeader = httpContext.Request.Headers.Authorization.ToString();
            await handler.HandleAsync(authorizationHeader, ct);
            return Results.NoContent();
        })
        .WithName("Logout")
        .WithTags("Auth")
        .AllowAnonymous();
    }
}
