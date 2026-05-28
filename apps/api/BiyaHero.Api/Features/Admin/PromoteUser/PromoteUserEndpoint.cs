namespace BiyaHero.Api.Features.Admin.PromoteUser;

/// <summary>
/// Maps POST /v1/admin/users/{id}/:promote to the PromoteUserHandler.
/// Thin endpoint — delegates all business logic to the handler.
/// Requires authentication (JWT Bearer token) + SuperAdmin role + password re-entry as 2FA.
/// Uses custom method syntax (colon prefix) per REST API steering.
/// Requirements: 5.9, 5.11
/// </summary>
public static class PromoteUserEndpoint
{
    public static void MapPromoteUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/admin/users/{id}/:promote", async (
            HttpContext context,
            Guid id,
            PromoteUserRequest request,
            PromoteUserHandler handler,
            CancellationToken ct) =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var response = await handler.HandleAsync(authHeader, id, request, ct);
            return Results.Ok(response);
        })
        .WithName("PromoteUser")
        .WithTags("Admin");
    }
}
