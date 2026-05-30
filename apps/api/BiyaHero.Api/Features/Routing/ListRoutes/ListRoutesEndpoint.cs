namespace BiyaHero.Api.Features.Routing.ListRoutes;

/// <summary>
/// Maps GET /v1/routes?bbox_sw_lat=X&amp;bbox_sw_lng=X&amp;bbox_ne_lat=X&amp;bbox_ne_lng=X to the ListRoutesHandler.
/// Returns { routes: [...] } with full waypoints for each route.
/// No authentication required (public data).
/// Requirements: 1.2
/// </summary>
public static class ListRoutesEndpoint
{
    public static void MapListRoutesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/routes", async (
            double? bbox_sw_lat,
            double? bbox_sw_lng,
            double? bbox_ne_lat,
            double? bbox_ne_lng,
            ListRoutesHandler handler) =>
        {
            // Also support legacy minLat/minLng/maxLat/maxLng params
            if (bbox_sw_lat is null || bbox_sw_lng is null || bbox_ne_lat is null || bbox_ne_lng is null)
            {
                return Results.UnprocessableEntity(new
                {
                    error = new
                    {
                        code = "input.validation_failed",
                        message = "All bounding box parameters are required: bbox_sw_lat, bbox_sw_lng, bbox_ne_lat, bbox_ne_lng."
                    }
                });
            }

            var results = await handler.HandleAsync(
                bbox_sw_lat.Value, bbox_sw_lng.Value, bbox_ne_lat.Value, bbox_ne_lng.Value);

            return Results.Ok(new { routes = results });
        })
        .WithName("ListRoutes")
        .WithTags("Routing")
        .AllowAnonymous();
    }
}
