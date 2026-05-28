namespace BiyaHero.Api.Features.Payment.Webhook;

/// <summary>
/// Maps POST /v1/payments/webhook to the WebhookHandler.
/// Thin HTTP boundary — reads raw body bytes and headers for signature verification,
/// delegates all business logic to the handler.
///
/// No JWT authentication — uses webhook signature verification instead.
///
/// Requirements: 3.1, 3.5, 3.7
/// </summary>
public static class WebhookEndpoint
{
    public static void MapWebhookEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/payments/webhook", async (HttpContext context, WebhookHandler handler, CancellationToken ct) =>
        {
            // Read raw body bytes for signature verification
            context.Request.EnableBuffering();
            using var memoryStream = new MemoryStream();
            await context.Request.Body.CopyToAsync(memoryStream, ct);
            var rawBody = memoryStream.ToArray();

            // Extract signature and timestamp headers
            var signature = context.Request.Headers["X-Wallet-Signature"].FirstOrDefault();
            var timestamp = context.Request.Headers["X-Wallet-Timestamp"].FirstOrDefault();

            // Delegate to handler
            var result = await handler.HandleAsync(rawBody, signature, timestamp, ct);

            if (result.IsUnauthorized)
            {
                return Results.Json(
                    new { error = new { code = "payment.signature_invalid", message = result.ErrorMessage } },
                    statusCode: 401);
            }

            return Results.Ok(new WebhookResponse(Received: true));
        })
        .WithName("PaymentWebhook")
        .WithTags("Payment")
        .AllowAnonymous();
    }
}
