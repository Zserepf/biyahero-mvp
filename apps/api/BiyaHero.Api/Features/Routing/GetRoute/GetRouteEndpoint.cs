namespace BiyaHero.Api.Features.Routing.GetRoute;

/// <summary>
/// Maps GET /v1/routes/{id} to the GetRouteHandler.
/// Thin HTTP boundary — parses the route ID, delegates to the handler.
/// 
/// No authentication required (public data).
/// Returns 404 if the route does not exist.
/// 
/// Requirements: 1.2
/// </summary>
public static class GetRouteEndpoint
{
    public static void MapGetRouteEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/routes/{id:guid}", async (Guid id, GetRouteHandler handler) =>
        {
            var result = await handler.HandleAsync(id);

            if (result is null)
            {
                return Results.NotFound(new
                {
                    error = new
                    {
                        code = "resource.not_found",
                        message = $"Route with id '{id}' was not found."
                    }
                });
            }

            return Results.Ok(result);
        })
        .WithName("GetRoute")
        .WithTags("Routing")
        .AllowAnonymous();
    }
}
