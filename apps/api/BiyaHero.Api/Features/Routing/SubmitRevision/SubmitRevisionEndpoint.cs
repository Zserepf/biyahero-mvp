namespace BiyaHero.Api.Features.Routing.SubmitRevision;

/// <summary>
/// Maps POST /v1/routes/{id}/revisions to the SubmitRevisionHandler.
/// Thin endpoint — delegates all business logic to the handler.
/// Requires authentication (JWT Bearer token).
/// </summary>
public static class SubmitRevisionEndpoint
{
    public static void MapSubmitRevisionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/routes/{id}/revisions", async (
            Guid id,
            HttpContext context,
            SubmitRevisionRequest request,
            SubmitRevisionHandler handler,
            CancellationToken ct) =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var response = await handler.HandleAsync(id, authHeader, request, ct);
            return Results.Created($"/v1/routes/{id}/revisions/{response.RevisionId}", response);
        })
        .WithName("SubmitRevision")
        .WithTags("Routing");
    }
}
