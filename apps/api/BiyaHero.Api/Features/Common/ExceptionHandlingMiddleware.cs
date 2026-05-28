using BiyaHero.Api.Features.Common.Exceptions;

namespace BiyaHero.Api.Features.Common;

/// <summary>
/// Global exception handling middleware that catches BiyaHeroException subclasses
/// and translates them into the standard REST error envelope:
/// { "error": { "code": "...", "message": "...", "details": { ... } } }
///
/// Also catches unhandled exceptions and returns a generic 500 response
/// to prevent raw ASP.NET error pages from leaking to clients.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BiyaHeroException ex)
        {
            // Don't attempt to write an error response if the response has already started
            // (e.g., during a WebSocket upgrade or streaming response).
            if (context.Response.HasStarted) return;

            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";

            var errorBody = new ErrorResponse(
                new ErrorDetail(ex.Code, ex.Message, ex.Details));

            await context.Response.WriteAsJsonAsync(errorBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            // Don't attempt to write an error response if the response has already started.
            if (context.Response.HasStarted) return;

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var errorBody = new ErrorResponse(
                new ErrorDetail("server.internal_error",
                    "An unexpected error occurred. Please try again later.", null));

            await context.Response.WriteAsJsonAsync(errorBody);
        }
    }
}
