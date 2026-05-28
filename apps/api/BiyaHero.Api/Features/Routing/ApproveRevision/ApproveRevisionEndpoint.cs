namespace BiyaHero.Api.Features.Routing.ApproveRevision;

/// <summary>
/// Maps POST /v1/routes/{id}/revisions/{rid}/:approve to the ApproveRevisionHandler.
/// Thin endpoint — delegates all business logic to the handler.
/// Requires authentication (JWT Bearer token) + Moderator or SuperAdmin role.
/// Uses custom method syntax (colon prefix) per REST API steering.
/// </summary>
public static class ApproveRevisionEndpoint
{
    public static void MapApproveRevisionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/routes/{id}/revisions/{rid}/:approve", async (
            HttpContext context,
            Guid id,
            Guid rid,
            ApproveRevisionHandler handler,
            CancellationToken ct) =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var response = await handler.HandleAsync(authHeader, id, rid, ct);
            return Results.Ok(response);
        })
        .WithName("ApproveRevision")
        .WithTags("Routing");
    }
}
