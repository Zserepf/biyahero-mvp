namespace BiyaHero.Api.Features.Routing.ListRoutes;

/// <summary>
/// Maps GET /v1/routes?minLat=X&amp;minLng=X&amp;maxLat=X&amp;maxLng=X to the ListRoutesHandler.
/// Thin HTTP boundary — validates bbox query parameters, delegates to the handler.
/// 
/// No authentication required (public data).
/// 
/// Requirements: 1.2
/// </summary>
public static class ListRoutesEndpoint
{
    public static void MapListRoutesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/routes", async (
            double? minLat,
            double? minLng,
            double? maxLat,
            double? maxLng,
            ListRoutesHandler handler) =>
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

            // Validate latitude ranges (-90 to 90)
            if (minLat.Value < -90.0 || minLat.Value > 90.0 || maxLat.Value < -90.0 || maxLat.Value > 90.0)
            {
                return Results.UnprocessableEntity(new
                {
                    error = new
                    {
                        code = "input.validation_failed",
                        message = "Latitude must be between -90 and 90 degrees."
                    }
                });
            }

            // Validate longitude ranges (-180 to 180)
            if (minLng.Value < -180.0 || minLng.Value > 180.0 || maxLng.Value < -180.0 || maxLng.Value > 180.0)
            {
                return Results.UnprocessableEntity(new
                {
                    error = new
                    {
                        code = "input.validation_failed",
                        message = "Longitude must be between -180 and 180 degrees."
                    }
                });
            }

            // Validate min <= max
            if (minLat.Value > maxLat.Value)
            {
                return Results.UnprocessableEntity(new
                {
                    error = new
                    {
                        code = "input.validation_failed",
                        message = "minLat must be less than or equal to maxLat."
                    }
                });
            }

            if (minLng.Value > maxLng.Value)
            {
                return Results.UnprocessableEntity(new
                {
                    error = new
                    {
                        code = "input.validation_failed",
                        message = "minLng must be less than or equal to maxLng."
                    }
                });
            }

            var results = await handler.HandleAsync(
                minLat.Value, minLng.Value, maxLat.Value, maxLng.Value);

            return Results.Ok(results);
        })
        .WithName("ListRoutes")
        .WithTags("Routing")
        .AllowAnonymous();
    }
}
