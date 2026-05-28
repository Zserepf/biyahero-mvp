namespace BiyaHero.Api.Features.I18n;

/// <summary>
/// Maps POST /v1/i18n/missing-keys to log missing translation keys for translator backfill.
/// Fire-and-forget from the client's perspective — returns 200 OK immediately.
/// No authentication required (frontend reports missing keys before/after login).
/// Logs are routed to CloudWatch in production for translator backfill.
/// Requirements: 10.6
/// </summary>
public static class MissingKeysEndpoint
{
    public static void MapMissingKeysEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/i18n/missing-keys", (MissingKeysRequest? request, ILogger<MissingKeysHandler> logger) =>
        {
            // Validate request body is well-formed
            if (request is null || request.Keys is null || request.Keys.Count == 0)
            {
                return Results.BadRequest(new { error = new { code = "request.malformed", message = "Request body must contain a non-empty 'keys' array." } });
            }

            // Validate each entry has required fields
            foreach (var entry in request.Keys)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Locale))
                {
                    return Results.BadRequest(new { error = new { code = "request.malformed", message = "Each entry must have a non-empty 'key' and 'locale'." } });
                }
            }

            // Log each missing key as a structured event for CloudWatch translator backfill
            foreach (var entry in request.Keys)
            {
                logger.LogWarning(
                    "MissingTranslationKey: Key={TranslationKey} Locale={Locale} Context={Context}",
                    entry.Key,
                    entry.Locale,
                    entry.Context ?? "unknown");
            }

            return Results.Ok();
        })
        .WithName("ReportMissingKeys")
        .WithTags("I18n")
        .AllowAnonymous();
    }
}

/// <summary>
/// Marker class for ILogger category name in the missing-keys endpoint.
/// </summary>
public sealed class MissingKeysHandler { }
