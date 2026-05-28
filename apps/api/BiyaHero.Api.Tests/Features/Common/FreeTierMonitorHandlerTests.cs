using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.Runtime;
using BiyaHero.Api.Features.Common.FreeTierMonitor;
using Microsoft.Extensions.Logging;

namespace BiyaHero.Api.Tests.Features.Common;

public class FreeTierMonitorHandlerTests
{
    /// <summary>
    /// When projected usage > 80% AND actual usage >= 50%, the handler should flag the service as alarming.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenProjectedExceeds80AndActualAtOrAbove50_ShouldFlagAsAlarming()
    {
        // Arrange: simulate mid-month (day 15 of 30), with usage that projects to >80%
        // and actual is already >= 50%.
        // Lambda free tier: 1,000,000 requests/month.
        // If we're on day 15 and have used 600,000 requests:
        //   Actual: 600,000 / 1,000,000 = 60% (>= 50% ✓)
        //   Projected: (600,000 / 15) * 30 = 1,200,000 → 120% (> 80% ✓)
        var now = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);

        // Return 600,000 as the sum for Lambda Invocations
        var cloudWatch = new FakeCloudWatchClient(new Dictionary<string, double>
        {
            { "AWS/Lambda:Invocations", 600_000 }
        });

        var logger = new FakeLogger<FreeTierMonitorHandler>();
        var handler = new FreeTierMonitorHandler(cloudWatch, logger, timeProvider);

        // Act
        var results = await handler.HandleAsync();

        // Assert: Lambda Invocations should be alarming
        var lambdaInvocations = results.FirstOrDefault(r =>
            r.ServiceName == "Lambda" && r.MetricName == "Invocations");

        Assert.NotNull(lambdaInvocations);
        Assert.True(lambdaInvocations.IsAlarming);
        Assert.True(lambdaInvocations.ActualPercentage >= 50.0);
        Assert.True(lambdaInvocations.ProjectedPercentage > 80.0);
    }

    /// <summary>
    /// When projected usage <= 80%, the handler should NOT flag the service as alarming,
    /// even if actual usage is above 50%.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenProjectedBelow80_ShouldNotFlagAsAlarming()
    {
        // Arrange: day 28 of 30, with 700,000 Lambda requests used.
        //   Actual: 700,000 / 1,000,000 = 70% (>= 50% ✓)
        //   Projected: (700,000 / 28) * 30 = 750,000 → 75% (< 80% ✗)
        var now = new DateTimeOffset(2024, 6, 28, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);

        var cloudWatch = new FakeCloudWatchClient(new Dictionary<string, double>
        {
            { "AWS/Lambda:Invocations", 700_000 }
        });

        var logger = new FakeLogger<FreeTierMonitorHandler>();
        var handler = new FreeTierMonitorHandler(cloudWatch, logger, timeProvider);

        // Act
        var results = await handler.HandleAsync();

        // Assert: Lambda Invocations should NOT be alarming
        var lambdaInvocations = results.FirstOrDefault(r =>
            r.ServiceName == "Lambda" && r.MetricName == "Invocations");

        Assert.NotNull(lambdaInvocations);
        Assert.False(lambdaInvocations.IsAlarming);
    }

    /// <summary>
    /// When actual usage is below 50%, the handler should NOT flag as alarming,
    /// even if projected usage exceeds 80%.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenActualBelow50_ShouldNotFlagAsAlarming()
    {
        // Arrange: day 3 of 30, with 100,000 Lambda requests used.
        //   Actual: 100,000 / 1,000,000 = 10% (< 50% ✗)
        //   Projected: (100,000 / 3) * 30 = 1,000,000 → 100% (> 80% ✓)
        // Both conditions must be met, so this should NOT alarm.
        var now = new DateTimeOffset(2024, 6, 3, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);

        var cloudWatch = new FakeCloudWatchClient(new Dictionary<string, double>
        {
            { "AWS/Lambda:Invocations", 100_000 }
        });

        var logger = new FakeLogger<FreeTierMonitorHandler>();
        var handler = new FreeTierMonitorHandler(cloudWatch, logger, timeProvider);

        // Act
        var results = await handler.HandleAsync();

        // Assert: Lambda Invocations should NOT be alarming (actual < 50%)
        var lambdaInvocations = results.FirstOrDefault(r =>
            r.ServiceName == "Lambda" && r.MetricName == "Invocations");

        Assert.NotNull(lambdaInvocations);
        Assert.False(lambdaInvocations.IsAlarming);
    }

    /// <summary>
    /// When CloudWatch returns zero usage, nothing should alarm.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNoUsage_ShouldNotFlagAnyServiceAsAlarming()
    {
        var now = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);

        // All metrics return 0
        var cloudWatch = new FakeCloudWatchClient(new Dictionary<string, double>());
        var logger = new FakeLogger<FreeTierMonitorHandler>();
        var handler = new FreeTierMonitorHandler(cloudWatch, logger, timeProvider);

        // Act
        var results = await handler.HandleAsync();

        // Assert: no services should be alarming
        Assert.All(results, r => Assert.False(r.IsAlarming));
    }

    /// <summary>
    /// The handler should return results for all monitored services.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnResultsForAllMonitoredServices()
    {
        var now = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var cloudWatch = new FakeCloudWatchClient(new Dictionary<string, double>());
        var logger = new FakeLogger<FreeTierMonitorHandler>();
        var handler = new FreeTierMonitorHandler(cloudWatch, logger, timeProvider);

        // Act
        var results = await handler.HandleAsync();

        // Assert: should have results for DynamoDB (2), Lambda (2), ApiGateway (1), SES (1), KMS (1) = 7
        Assert.Equal(7, results.Count);
        Assert.Contains(results, r => r.ServiceName == "DynamoDB");
        Assert.Contains(results, r => r.ServiceName == "Lambda");
        Assert.Contains(results, r => r.ServiceName == "ApiGateway");
        Assert.Contains(results, r => r.ServiceName == "SES");
        Assert.Contains(results, r => r.ServiceName == "KMS");
    }

    /// <summary>
    /// When CloudWatch throws an exception for a metric, the handler should treat usage as 0
    /// and continue checking other services.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCloudWatchThrows_ShouldTreatAsZeroAndContinue()
    {
        var now = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var cloudWatch = new FakeCloudWatchClient(
            new Dictionary<string, double>(),
            throwOnMetric: "AWS/Lambda:Invocations");
        var logger = new FakeLogger<FreeTierMonitorHandler>();
        var handler = new FreeTierMonitorHandler(cloudWatch, logger, timeProvider);

        // Act
        var results = await handler.HandleAsync();

        // Assert: should still return all results, Lambda Invocations treated as 0
        Assert.Equal(7, results.Count);
        var lambdaInvocations = results.First(r =>
            r.ServiceName == "Lambda" && r.MetricName == "Invocations");
        Assert.Equal(0, lambdaInvocations.ActualUsage);
        Assert.False(lambdaInvocations.IsAlarming);
    }
}

#region Test Doubles

/// <summary>
/// Fake TimeProvider for deterministic testing.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>
/// Fake CloudWatch client that returns preconfigured metric values.
/// Keys are "{Namespace}:{MetricName}".
/// </summary>
internal sealed class FakeCloudWatchClient : IAmazonCloudWatch
{
    private readonly Dictionary<string, double> _metricValues;
    private readonly string? _throwOnMetric;

    public FakeCloudWatchClient(Dictionary<string, double> metricValues, string? throwOnMetric = null)
    {
        _metricValues = metricValues;
        _throwOnMetric = throwOnMetric;
    }

    public Task<GetMetricStatisticsResponse> GetMetricStatisticsAsync(
        GetMetricStatisticsRequest request,
        CancellationToken cancellationToken = default)
    {
        var key = $"{request.Namespace}:{request.MetricName}";

        if (_throwOnMetric == key)
        {
            throw new AmazonCloudWatchException("Simulated CloudWatch failure");
        }

        var sum = _metricValues.TryGetValue(key, out var value) ? value : 0.0;

        var response = new GetMetricStatisticsResponse
        {
            Datapoints = new List<Datapoint>
            {
                new Datapoint { Sum = sum }
            }
        };

        return Task.FromResult(response);
    }

    // Minimal IAmazonCloudWatch implementation — only GetMetricStatisticsAsync is used
    public ICloudWatchPaginatorFactory Paginators => throw new NotImplementedException();
    public IClientConfig Config => throw new NotImplementedException();

    public void Dispose() { }

    public Task<DeleteAlarmsResponse> DeleteAlarmsAsync(DeleteAlarmsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DeleteAnomalyDetectorResponse> DeleteAnomalyDetectorAsync(DeleteAnomalyDetectorRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DeleteDashboardsResponse> DeleteDashboardsAsync(DeleteDashboardsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DeleteInsightRulesResponse> DeleteInsightRulesAsync(DeleteInsightRulesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DeleteMetricStreamResponse> DeleteMetricStreamAsync(DeleteMetricStreamRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeAlarmHistoryResponse> DescribeAlarmHistoryAsync(DescribeAlarmHistoryRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeAlarmHistoryResponse> DescribeAlarmHistoryAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeAlarmsResponse> DescribeAlarmsAsync(DescribeAlarmsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeAlarmsResponse> DescribeAlarmsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeAlarmsForMetricResponse> DescribeAlarmsForMetricAsync(DescribeAlarmsForMetricRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeAnomalyDetectorsResponse> DescribeAnomalyDetectorsAsync(DescribeAnomalyDetectorsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DescribeInsightRulesResponse> DescribeInsightRulesAsync(DescribeInsightRulesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DisableAlarmActionsResponse> DisableAlarmActionsAsync(DisableAlarmActionsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DisableInsightRulesResponse> DisableInsightRulesAsync(DisableInsightRulesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<EnableAlarmActionsResponse> EnableAlarmActionsAsync(EnableAlarmActionsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<EnableInsightRulesResponse> EnableInsightRulesAsync(EnableInsightRulesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<GetDashboardResponse> GetDashboardAsync(GetDashboardRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<GetInsightRuleReportResponse> GetInsightRuleReportAsync(GetInsightRuleReportRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<GetMetricDataResponse> GetMetricDataAsync(GetMetricDataRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<GetMetricStreamResponse> GetMetricStreamAsync(GetMetricStreamRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<GetMetricWidgetImageResponse> GetMetricWidgetImageAsync(GetMetricWidgetImageRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListDashboardsResponse> ListDashboardsAsync(ListDashboardsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListManagedInsightRulesResponse> ListManagedInsightRulesAsync(ListManagedInsightRulesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListMetricsResponse> ListMetricsAsync(ListMetricsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListMetricsResponse> ListMetricsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListMetricStreamsResponse> ListMetricStreamsAsync(ListMetricStreamsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListTagsForResourceResponse> ListTagsForResourceAsync(ListTagsForResourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutAnomalyDetectorResponse> PutAnomalyDetectorAsync(PutAnomalyDetectorRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutCompositeAlarmResponse> PutCompositeAlarmAsync(PutCompositeAlarmRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutDashboardResponse> PutDashboardAsync(PutDashboardRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutInsightRuleResponse> PutInsightRuleAsync(PutInsightRuleRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutManagedInsightRulesResponse> PutManagedInsightRulesAsync(PutManagedInsightRulesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutMetricAlarmResponse> PutMetricAlarmAsync(PutMetricAlarmRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutMetricDataResponse> PutMetricDataAsync(PutMetricDataRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutMetricStreamResponse> PutMetricStreamAsync(PutMetricStreamRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<SetAlarmStateResponse> SetAlarmStateAsync(SetAlarmStateRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<StartMetricStreamsResponse> StartMetricStreamsAsync(StartMetricStreamsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<StopMetricStreamsResponse> StopMetricStreamsAsync(StopMetricStreamsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<TagResourceResponse> TagResourceAsync(TagResourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<UntagResourceResponse> UntagResourceAsync(UntagResourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Amazon.Runtime.Endpoints.Endpoint DetermineServiceOperationEndpoint(AmazonWebServiceRequest request) => throw new NotImplementedException();
}

/// <summary>
/// Minimal ILogger implementation for testing that captures log entries.
/// </summary>
internal sealed class FakeLogger<T> : ILogger<T>
{
    public List<string> LogEntries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LogEntries.Add($"[{logLevel}] {formatter(state, exception)}");
    }
}

#endregion
