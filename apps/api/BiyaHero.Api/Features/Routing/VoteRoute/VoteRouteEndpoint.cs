namespace BiyaHero.Api.Features.Routing.VoteRoute;

/// <summary>
/// Maps POST /v1/routes/{id}/votes to the VoteRouteHandler.
/// Thin endpoint — delegates all business logic to the handler.
/// Requires authentication (JWT Bearer token).
/// Returns 201 Created on success, 404 if route not found, 409 on duplicate vote.
/// 
/// Requirements: 1.5, 1.6
/// </summary>
public static class VoteRouteEndpoint
{
    public static void MapVoteRouteEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/routes/{id:guid}/votes", async (
            Guid id,
            HttpContext context,
            VoteRouteRequest request,
            VoteRouteHandler handler,
            CancellationToken ct) =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var response = await handler.HandleAsync(id, authHeader, request, ct);

            if (response is null)
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

            return Results.Created($"/v1/routes/{id}/votes/{response.VoteId}", response);
        })
        .WithName("VoteRoute")
        .WithTags("Routing");
    }
}
