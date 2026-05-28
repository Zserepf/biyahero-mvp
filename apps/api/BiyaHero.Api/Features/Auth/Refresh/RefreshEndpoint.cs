namespace BiyaHero.Api.Features.Auth.Refresh;

/// <summary>
/// Maps POST /v1/auth/sessions/:refresh to the RefreshHandler.
/// Custom method per REST steering (colon syntax for non-CRUD operations).
/// Thin endpoint — delegates all business logic to the handler.
/// </summary>
public static class RefreshEndpoint
{
    public static void MapRefreshEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/auth/sessions/:refresh", async (RefreshRequest request, RefreshHandler handler, CancellationToken ct) =>
        {
            var response = await handler.HandleAsync(request, ct);
            return Results.Ok(response);
        })
        .WithName("RefreshToken")
        .WithTags("Auth")
        .AllowAnonymous();
    }
}
