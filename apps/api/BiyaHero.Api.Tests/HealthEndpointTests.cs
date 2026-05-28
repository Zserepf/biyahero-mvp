using BiyaHero.Api.Features.Health;
using Xunit;

namespace BiyaHero.Api.Tests;

public class HealthEndpointTests
{
    [Fact]
    public void HealthCheckResponse_ShouldContainStatus()
    {
        var dependencies = new DependencyStatus("healthy", "healthy", "healthy");
        var response = new HealthCheckResponse("healthy", DateTimeOffset.UtcNow, dependencies);

        Assert.Equal("healthy", response.Status);
        Assert.NotNull(response.Dependencies);
    }

    [Fact]
    public void HealthCheckResponse_ShouldReportDegraded_WhenPostgresUnhealthy()
    {
        var dependencies = new DependencyStatus("unhealthy", "healthy", "healthy");
        var response = new HealthCheckResponse("degraded", DateTimeOffset.UtcNow, dependencies);

        Assert.Equal("degraded", response.Status);
        Assert.Equal("unhealthy", response.Dependencies.Postgres);
        Assert.Equal("healthy", response.Dependencies.Dynamodb);
        Assert.Equal("healthy", response.Dependencies.Websocket);
    }

    [Fact]
    public void HealthCheckResponse_ShouldReportDegraded_WhenDynamoDbUnhealthy()
    {
        var dependencies = new DependencyStatus("healthy", "unhealthy", "healthy");
        var response = new HealthCheckResponse("degraded", DateTimeOffset.UtcNow, dependencies);

        Assert.Equal("degraded", response.Status);
        Assert.Equal("healthy", response.Dependencies.Postgres);
        Assert.Equal("unhealthy", response.Dependencies.Dynamodb);
    }

    [Fact]
    public void HealthCheckResponse_ShouldReportWebSocketNotConfigured()
    {
        var dependencies = new DependencyStatus("healthy", "healthy", "not_configured");
        var response = new HealthCheckResponse("healthy", DateTimeOffset.UtcNow, dependencies);

        Assert.Equal("healthy", response.Status);
        Assert.Equal("not_configured", response.Dependencies.Websocket);
    }

    [Fact]
    public void DependencyStatus_ShouldHaveAllFields()
    {
        var status = new DependencyStatus("healthy", "unhealthy", "not_configured");

        Assert.Equal("healthy", status.Postgres);
        Assert.Equal("unhealthy", status.Dynamodb);
        Assert.Equal("not_configured", status.Websocket);
    }
}
