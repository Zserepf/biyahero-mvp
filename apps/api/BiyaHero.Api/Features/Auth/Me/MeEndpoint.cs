namespace BiyaHero.Api.Features.Auth.Me;

/// <summary>
/// Maps GET /v1/auth/me to the MeHandler.
/// Thin endpoint — delegates all business logic to the handler.
/// </summary>
public static class MeEndpoint
{
    public static void MapMeEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/auth/me", async (HttpContext context, MeHandler handler, CancellationToken ct) =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var response = await handler.GetMeAsync(authHeader, ct);
            return Results.Ok(response);
        })
        .WithName("GetMe")
        .WithTags("Auth");
    }
}
