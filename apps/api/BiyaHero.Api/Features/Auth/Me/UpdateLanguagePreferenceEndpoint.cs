namespace BiyaHero.Api.Features.Auth.Me;

/// <summary>
/// Maps PATCH /v1/auth/me/language-preference to the MeHandler.
/// Thin endpoint — delegates all business logic to the handler.
/// </summary>
public static class UpdateLanguagePreferenceEndpoint
{
    public static void MapUpdateLanguagePreferenceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/v1/auth/me/language-preference", async (
            HttpContext context,
            UpdateLanguagePreferenceRequest request,
            MeHandler handler,
            CancellationToken ct) =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var response = await handler.UpdateLanguagePreferenceAsync(authHeader, request, ct);
            return Results.Ok(response);
        })
        .WithName("UpdateLanguagePreference")
        .WithTags("Auth");
    }
}
