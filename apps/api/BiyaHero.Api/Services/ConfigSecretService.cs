using System.Text;

namespace BiyaHero.Api.Services;

/// <summary>
/// Configuration-based secret service for development and testing.
/// Reads secrets from IConfiguration. In production, replace with a KMS-backed implementation.
/// </summary>
public sealed class ConfigSecretService : ISecretService
{
    private const string DefaultJwtKey = "BiyaHero-Dev-JWT-Secret-Key-Must-Be-At-Least-32-Bytes!";
    private const string DefaultWebhookKey = "BiyaHero-Dev-Webhook-Secret-Key-32B!";

    private readonly byte[] _jwtSigningKey;
    private readonly byte[] _webhookSigningSecret;

    public ConfigSecretService(IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:SigningKey"] ?? DefaultJwtKey;
        var webhookKey = configuration["Webhook:SigningSecret"] ?? DefaultWebhookKey;

        _jwtSigningKey = Encoding.UTF8.GetBytes(jwtKey);
        _webhookSigningSecret = Encoding.UTF8.GetBytes(webhookKey);
    }

    public Task<byte[]> GetJwtSigningKeyAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_jwtSigningKey);

    public Task<byte[]> GetWebhookSigningSecretAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_webhookSigningSecret);
}
