namespace BiyaHero.Api.Features.Health;

/// <summary>
/// Response shape for the /v1/health endpoint.
/// Always returned with HTTP 200 — the body indicates per-dependency health.
///
/// Requirements: 7.6
/// </summary>
public record HealthCheckResponse(
    string Status,
    DateTimeOffset Timestamp,
    DependencyStatus Dependencies);

public record DependencyStatus(
    string Postgres,
    string Dynamodb,
    string Websocket);
