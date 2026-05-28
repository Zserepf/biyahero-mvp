namespace BiyaHero.Api.Features.Admin.ListUsers;

/// <summary>
/// Maps GET /v1/admin/users to the ListUsersHandler.
/// Thin endpoint — delegates all business logic to the handler.
/// Requires authentication (JWT Bearer token) + SuperAdmin role.
/// Requirements: 5.8, 5.9
/// </summary>
public static class ListUsersEndpoint
{
    public static void MapListUsersEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/admin/users", async (
            HttpContext context,
            ListUsersHandler handler,
            CancellationToken ct) =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var response = await handler.HandleAsync(authHeader, ct);
            return Results.Ok(response);
        })
        .WithName("ListUsers")
        .WithTags("Admin");
    }
}
