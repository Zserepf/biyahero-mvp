namespace BiyaHero.Api.Features.Admin.SuspendUser;

/// <summary>
/// Maps POST /v1/admin/users/{id}/:suspend to the SuspendUserHandler.
/// Thin endpoint — delegates all business logic to the handler.
/// Requires authentication (JWT Bearer token) + SuperAdmin role.
/// Uses custom method syntax (colon prefix) per REST API steering.
/// Requirements: 5.8
/// </summary>
public static class SuspendUserEndpoint
{
    public static void MapSuspendUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/admin/users/{id}/:suspend", async (
            HttpContext context,
            Guid id,
            SuspendUserHandler handler,
            CancellationToken ct) =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var response = await handler.HandleAsync(authHeader, id, ct);
            return Results.Ok(response);
        })
        .WithName("SuspendUser")
        .WithTags("Admin");
    }
}
