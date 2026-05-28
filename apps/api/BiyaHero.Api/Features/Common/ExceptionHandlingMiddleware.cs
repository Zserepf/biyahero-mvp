using BiyaHero.Api.Features.Common.Exceptions;

namespace BiyaHero.Api.Features.Common;

/// <summary>
/// Global exception handling middleware that catches BiyaHeroException subclasses
/// and translates them into the standard REST error envelope:
/// { "error": { "code": "...", "message": "...", "details": { ... } } }
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BiyaHeroException ex)
        {
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";

            var errorBody = new
            {
                error = new
                {
                    code = ex.Code,
                    message = ex.Message,
                    details = ex.Details
                }
            };

            await context.Response.WriteAsJsonAsync(errorBody);
        }
    }
}
