namespace BiyaHero.Api.Features.Fare;

/// <summary>
/// Maps POST /v1/fare/:calculate to the FareCalculateHandler.
/// Thin HTTP boundary — validates request shape, delegates business logic to the handler,
/// and translates results to appropriate HTTP status codes.
/// 
/// No authentication required for fare calculation (anonymous access).
/// 
/// Status code semantics:
///   400 — Malformed request: missing required fields, null body, wrong content-type
///   422 — Structurally valid but semantically invalid: coordinates out of range, unsupported vehicle type
///   200 — Success with fare result
/// 
/// Requirements: 2.1, 2.6, 2.7, 2.9
/// </summary>
public static class FareCalculateEndpoint
{
    public static void MapFareCalculateEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/fare/:calculate", async (FareCalculateRequest? request, FareCalculateHandler handler) =>
        {
            // 400 for null body (missing body or unparseable JSON)
            if (request is null)
            {
                return Results.BadRequest(new
                {
                    error = new
                    {
                        code = "request.malformed",
                        message = "Request body is required."
                    }
                });
            }

            // 400 for missing required fields (null values on required properties)
            var missingFields = GetMissingFields(request);
            if (missingFields.Count > 0)
            {
                return Results.BadRequest(new
                {
                    error = new
                    {
                        code = "request.malformed",
                        message = $"Missing required fields: {string.Join(", ", missingFields)}."
                    }
                });
            }

            // At this point all required fields are present — extract non-null values
            double originLat = request.OriginLat!.Value;
            double originLng = request.OriginLng!.Value;
            double destinationLat = request.DestinationLat!.Value;
            double destinationLng = request.DestinationLng!.Value;
            string vehicleType = request.VehicleType!;

            // 422 for invalid coordinates
            if (!IsValidLatitude(originLat) || !IsValidLatitude(destinationLat))
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

            if (!IsValidLongitude(originLng) || !IsValidLongitude(destinationLng))
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

            var response = await handler.HandleAsync(request);

            // 422 for unsupported vehicle type (matrix not found) — no fare in response
            if (response is null)
            {
                return Results.UnprocessableEntity(new
                {
                    error = new
                    {
                        code = "input.validation_failed",
                        message = $"Unsupported vehicle type: '{vehicleType}'."
                    }
                });
            }

            return Results.Ok(response);
        })
        .WithName("FareCalculate")
        .WithTags("Fare")
        .AllowAnonymous();
    }

    /// <summary>
    /// Checks which required fields are missing (null) in the request.
    /// Returns an empty list if all required fields are present.
    /// </summary>
    private static List<string> GetMissingFields(FareCalculateRequest request)
    {
        var missing = new List<string>();

        if (request.OriginLat is null) missing.Add("originLat");
        if (request.OriginLng is null) missing.Add("originLng");
        if (request.DestinationLat is null) missing.Add("destinationLat");
        if (request.DestinationLng is null) missing.Add("destinationLng");
        if (string.IsNullOrWhiteSpace(request.VehicleType)) missing.Add("vehicleType");

        return missing;
    }

    private static bool IsValidLatitude(double lat) => lat >= -90.0 && lat <= 90.0;
    private static bool IsValidLongitude(double lng) => lng >= -180.0 && lng <= 180.0;
}
