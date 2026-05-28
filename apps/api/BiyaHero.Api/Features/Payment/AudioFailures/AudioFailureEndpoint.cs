using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Payment.AudioFailures;

/// <summary>
/// Maps POST /v1/payments/audio-failures to log driver-side audio playback failures.
/// Fire-and-forget from the client's perspective — returns 200 OK.
/// Requires authentication (only authenticated drivers should report audio failures).
///
/// Status code semantics:
///   200 — Success: failure event logged to CloudWatch
///   400 — Malformed request: missing required fields, null body, invalid reason
///   401 — Unauthenticated: missing or invalid JWT
///
/// Requirements: 3.8
/// </summary>
public static class AudioFailureEndpoint
{
    public static void MapAudioFailureEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/payments/audio-failures", async (
            HttpContext context,
            AudioFailureRequest? request,
            IJwtService jwtService,
            ILogger<AudioFailureHandler> logger,
            CancellationToken ct) =>
        {
            // Validate JWT — only authenticated users can report audio failures
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authHeader) ||
                !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(
                    new { error = new { code = "auth.unauthenticated", message = "Authentication required." } },
                    statusCode: 401);
            }

            var token = authHeader["Bearer ".Length..].Trim();
            var principal = await jwtService.ValidateTokenAsync(token, ct);
            if (principal is null)
            {
                return Results.Json(
                    new { error = new { code = "auth.unauthenticated", message = "Authentication required." } },
                    statusCode: 401);
            }

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

            // 400 for missing required fields
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

            // 400 for invalid reason value
            if (!AudioFailureRequest.ValidReasons.Contains(request.Reason!))
            {
                return Results.BadRequest(new
                {
                    error = new
                    {
                        code = "request.malformed",
                        message = $"Invalid reason '{request.Reason}'. Valid values: muted, autoplay_blocked, voice_unavailable."
                    }
                });
            }

            // Log the audio failure event for CloudWatch observability
            logger.LogWarning(
                "Audio playback failure for driver '{DriverId}', payment event '{PaymentEventId}': {Reason} (UA: {UserAgent})",
                request.DriverId,
                request.PaymentEventId,
                request.Reason,
                request.UserAgent);

            return Results.Ok();
        })
        .WithName("ReportAudioFailure")
        .WithTags("Payment");
    }

    /// <summary>
    /// Checks which required fields are missing (null/empty) in the request.
    /// DriverId and Reason are required; PaymentEventId and UserAgent are optional.
    /// </summary>
    private static List<string> GetMissingFields(AudioFailureRequest request)
    {
        var missing = new List<string>();

        if (request.DriverId is null || request.DriverId == Guid.Empty)
            missing.Add("driverId");
        if (string.IsNullOrWhiteSpace(request.Reason))
            missing.Add("reason");

        return missing;
    }
}

/// <summary>
/// Marker class for ILogger category name in the audio-failure endpoint.
/// </summary>
public sealed class AudioFailureHandler { }
