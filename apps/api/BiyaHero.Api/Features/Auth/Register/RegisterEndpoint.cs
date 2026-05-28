namespace BiyaHero.Api.Features.Auth.Register;

/// <summary>
/// Maps the registration endpoint: POST /v1/auth/registrations
/// Thin HTTP boundary — delegates all logic to RegisterHandler.
/// No authentication required.
/// Requirements: 5.1, 5.5
/// </summary>
public static class RegisterEndpoint
{
    public static void MapRegisterEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/auth/registrations", async (
            RegisterRequest request,
            RegisterHandler handler) =>
        {
            var response = await handler.HandleAsync(request);
            return Results.Created($"/v1/auth/registrations/{response.UserId}", response);
        })
        .WithName("Register")
        .WithTags("Auth")
        .AllowAnonymous();
    }
}
