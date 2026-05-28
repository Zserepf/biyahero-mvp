namespace BiyaHero.Api.Features.Heatmap.GetTiles;

/// <summary>
/// Maps GET /v1/heatmap/tiles to the GetTilesHandler.
/// Thin HTTP boundary — parses query parameters, delegates to handler, returns response.
/// 
/// No authentication required — anonymous read allowed (Requirement 4.2, 4.6, 4.7).
/// 
/// Query parameters:
///   minLat, minLng, maxLat, maxLng (required) — bounding box coordinates
///   vehicleType (optional) — filter tiles by vehicle type
/// </summary>
public static class GetTilesEndpoint
{
    public static void MapGetTilesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/heatmap/tiles", async (
            double? minLat,
            double? minLng,
            double? maxLat,
            double? maxLng,
            string? vehicleType,
            GetTilesHandler handler,
            CancellationToken cancellationToken) =>
        {
            // Validate all 4 bbox parameters are present
            if (minLat is null || minLng is null || maxLat is null || maxLng is null)
            {
                return Results.UnprocessableEntity(new
                {
                    error = new
                    {
                        code = "input.validation_failed",
                        message = "All bounding box parameters are required: minLat, minLng, maxLat, maxLng."
                    }
                });
            }

            var result = await handler.HandleAsync(
                minLat.Value,
                minLng.Value,
                maxLat.Value,
                maxLng.Value,
                vehicleType,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return Results.UnprocessableEntity(new
                {
                    error = new
                    {
                        code = "input.validation_failed",
                        message = result.ErrorMessage
                    }
                });
            }

            return Results.Ok(result.Tiles);
        })
        .WithName("GetHeatmapTiles")
        .WithTags("Heatmap")
        .AllowAnonymous();
    }
}
