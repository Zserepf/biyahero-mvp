namespace BiyaHero.Api.Features.Health;

/// <summary>
/// Maps the GET /v1/health endpoint.
///
/// Requirements: 7.6
/// </summary>
public static class HealthCheckEndpoint
{
    public static void MapHealthCheckEndpoint(this WebApplication app)
    {
        app.MapGet("/v1/health", async (HealthCheckHandler handler) =>
        {
            var response = await handler.HandleAsync();
            return Results.Ok(response);
        });
    }
}
