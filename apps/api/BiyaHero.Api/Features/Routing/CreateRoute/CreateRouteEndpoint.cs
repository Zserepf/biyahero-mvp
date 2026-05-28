namespace BiyaHero.Api.Features.Routing.CreateRoute;

/// <summary>
/// Maps POST /v1/routes to the CreateRouteHandler.
/// Thin endpoint — delegates all business logic to the handler.
/// Requires authentication (JWT Bearer token).
/// </summary>
public static class CreateRouteEndpoint
{
    public static void MapCreateRouteEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/routes", async (
            HttpContext context,
            CreateRouteRequest request,
            CreateRouteHandler handler,
            CancellationToken ct) =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var response = await handler.HandleAsync(authHeader, request, ct);
            return Results.Created($"/v1/routes/{response.Id}", response);
        })
        .WithName("CreateRoute")
        .WithTags("Routing");
    }
}
