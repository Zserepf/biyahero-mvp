using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

namespace BiyaHero.Api.Features.Common.FreeTierMonitor;

/// <summary>
/// Monitors AWS free-tier usage and emits operational alerts when:
/// - Projected monthly usage exceeds 80% of the free-tier allowance, AND
/// - Actual measured usage is at or above 50% of the free-tier allowance.
///
/// Designed to be triggered by a CloudWatch Events/EventBridge schedule (daily or hourly).
/// Uses ILogger to emit warnings that route to CloudWatch Logs in production.
///
/// Requirements: 7.4 (stay within free tier), 7.5 (emit alert at 80% projected + 50% actual).
/// </summary>
public sealed class FreeTierMonitorHandler
{
    private readonly IAmazonCloudWatch _cloudWatch;
    private readonly ILogger<FreeTierMonitorHandler> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Threshold: projected monthly usage must exceed this percentage to trigger an alert.
    /// </summary>
    private const double ProjectedThresholdPercent = 80.0;

    /// <summary>
    /// Threshold: actual usage must be at or above this percentage to trigger an alert.
    /// </summary>
    private const double ActualThresholdPercent = 50.0;

    /// <summary>
    /// Free-tier limits for monitored AWS services.
    /// </summary>
    private static readonly FreeTierLimit[] MonitoredLimits =
    [
        // DynamoDB: 25 GB storage
        new FreeTierLimit(
            ServiceName: "DynamoDB",
            MetricName: "AccountProvisionedReadCapacityUtilization",
            Namespace: "AWS/DynamoDB",
            MonthlyAllowance: 25.0,
            Unit: "RCU",
            DimensionName: "",
            DimensionValue: ""),

        // DynamoDB: 25 WCU provisioned
        new FreeTierLimit(
            ServiceName: "DynamoDB",
            MetricName: "AccountProvisionedWriteCapacityUtilization",
            Namespace: "AWS/DynamoDB",
            MonthlyAllowance: 25.0,
            Unit: "WCU",
            DimensionName: "",
            DimensionValue: ""),

        // Lambda: 1,000,000 requests/month
        new FreeTierLimit(
            ServiceName: "Lambda",
            MetricName: "Invocations",
            Namespace: "AWS/Lambda",
            MonthlyAllowance: 1_000_000.0,
            Unit: "requests",
            DimensionName: "",
            DimensionValue: ""),

        // Lambda: 400,000 GB-seconds/month
        new FreeTierLimit(
            ServiceName: "Lambda",
            MetricName: "Duration",
            Namespace: "AWS/Lambda",
            MonthlyAllowance: 400_000.0,
            Unit: "GB-seconds",
            DimensionName: "",
            DimensionValue: ""),

        // API Gateway: 1,000,000 REST API calls/month
        new FreeTierLimit(
            ServiceName: "ApiGateway",
            MetricName: "Count",
            Namespace: "AWS/ApiGateway",
            MonthlyAllowance: 1_000_000.0,
            Unit: "requests",
            DimensionName: "",
            DimensionValue: ""),

        // SES: 62,000 emails/month (from EC2/Lambda)
        new FreeTierLimit(
            ServiceName: "SES",
            MetricName: "Send",
            Namespace: "AWS/SES",
            MonthlyAllowance: 62_000.0,
            Unit: "emails",
            DimensionName: "",
            DimensionValue: ""),

        // KMS: 20,000 requests/month
        new FreeTierLimit(
            ServiceName: "KMS",
            MetricName: "CallCount",
            Namespace: "AWS/KMS",
            MonthlyAllowance: 20_000.0,
            Unit: "requests",
            DimensionName: "",
            DimensionValue: ""),
    ];

    public FreeTierMonitorHandler(
        IAmazonCloudWatch cloudWatch,
        ILogger<FreeTierMonitorHandler> logger,
        TimeProvider timeProvider)
    {
        _cloudWatch = cloudWatch;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Executes the free-tier usage check for all monitored services.
    /// Called on a schedule (EventBridge rule, e.g., daily or hourly).
    /// </summary>
    public async Task<List<UsageCheckResult>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var daysElapsed = (now - startOfMonth).TotalDays;

        // Avoid division by zero on the first instant of the month
        if (daysElapsed < 0.01)
        {
            daysElapsed = 0.01;
        }

        _logger.LogInformation(
            "FreeTierMonitor: Starting usage check. Month: {Month}/{Year}, DaysElapsed: {DaysElapsed:F2}, DaysInMonth: {DaysInMonth}",
            now.Month, now.Year, daysElapsed, daysInMonth);

        var results = new List<UsageCheckResult>();

        foreach (var limit in MonitoredLimits)
        {
            var result = await CheckServiceUsageAsync(limit, startOfMonth, now.UtcDateTime, daysElapsed, daysInMonth, cancellationToken);
            results.Add(result);

            if (result.IsAlarming)
            {
                _logger.LogWarning(
                    "FREE-TIER ALERT: {ServiceName}/{MetricName} — " +
                    "Actual: {ActualUsage:F0}/{MonthlyAllowance:F0} {Unit} ({ActualPercent:F1}%), " +
                    "Projected: {ProjectedUsage:F0}/{MonthlyAllowance2:F0} {Unit2} ({ProjectedPercent:F1}%). " +
                    "Projected exceeds 80% AND actual is at or above 50% of free-tier allowance.",
                    limit.ServiceName,
                    limit.MetricName,
                    result.ActualUsage,
                    result.MonthlyAllowance,
                    limit.Unit,
                    result.ActualPercentage,
                    result.ProjectedMonthlyUsage,
                    result.MonthlyAllowance,
                    limit.Unit,
                    result.ProjectedPercentage);
            }
            else
            {
                _logger.LogInformation(
                    "FreeTierMonitor: {ServiceName}/{MetricName} — " +
                    "Actual: {ActualUsage:F0}/{MonthlyAllowance:F0} ({ActualPercent:F1}%), " +
                    "Projected: {ProjectedUsage:F0}/{MonthlyAllowance2:F0} ({ProjectedPercent:F1}%) — OK",
                    limit.ServiceName,
                    limit.MetricName,
                    result.ActualUsage,
                    result.MonthlyAllowance,
                    result.ActualPercentage,
                    result.ProjectedMonthlyUsage,
                    result.MonthlyAllowance,
                    result.ProjectedPercentage);
            }
        }

        var alarmCount = results.Count(r => r.IsAlarming);
        if (alarmCount > 0)
        {
            _logger.LogWarning(
                "FreeTierMonitor: Check complete. {AlarmCount} service(s) triggered alerts out of {Total} monitored.",
                alarmCount, results.Count);
        }
        else
        {
            _logger.LogInformation(
                "FreeTierMonitor: Check complete. All {Total} monitored services within safe limits.",
                results.Count);
        }

        return results;
    }

    /// <summary>
    /// Checks a single service's usage against its free-tier limit.
    /// Queries CloudWatch for the sum of the metric from the start of the month to now,
    /// then projects the full-month usage based on the current rate.
    /// </summary>
    private async Task<UsageCheckResult> CheckServiceUsageAsync(
        FreeTierLimit limit,
        DateTime startOfMonth,
        DateTime now,
        double daysElapsed,
        int daysInMonth,
        CancellationToken cancellationToken)
    {
        double actualUsage;

        try
        {
            actualUsage = await GetMetricSumAsync(limit, startOfMonth, now, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "FreeTierMonitor: Failed to retrieve metric {Namespace}/{MetricName} for {ServiceName}. Treating as 0.",
                limit.Namespace, limit.MetricName, limit.ServiceName);
            actualUsage = 0;
        }

        // Project full-month usage: (actual / days elapsed) * days in month
        var projectedMonthlyUsage = (actualUsage / daysElapsed) * daysInMonth;
        var actualPercentage = (actualUsage / limit.MonthlyAllowance) * 100.0;
        var projectedPercentage = (projectedMonthlyUsage / limit.MonthlyAllowance) * 100.0;

        // Alert condition: projected > 80% AND actual >= 50%
        var isAlarming = projectedPercentage > ProjectedThresholdPercent
                         && actualPercentage >= ActualThresholdPercent;

        return new UsageCheckResult(
            ServiceName: limit.ServiceName,
            MetricName: limit.MetricName,
            MonthlyAllowance: limit.MonthlyAllowance,
            ActualUsage: actualUsage,
            ActualPercentage: actualPercentage,
            ProjectedMonthlyUsage: projectedMonthlyUsage,
            ProjectedPercentage: projectedPercentage,
            IsAlarming: isAlarming);
    }

    /// <summary>
    /// Queries CloudWatch for the Sum statistic of a metric over the given time range.
    /// Uses a single-period query spanning the entire month-to-date.
    /// </summary>
    private async Task<double> GetMetricSumAsync(
        FreeTierLimit limit,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        var request = new GetMetricStatisticsRequest
        {
            Namespace = limit.Namespace,
            MetricName = limit.MetricName,
            StartTime = startTime,
            EndTime = endTime,
            Period = (int)(endTime - startTime).TotalSeconds,
            Statistics = new List<string> { "Sum" }
        };

        // Add dimension filter if specified
        if (!string.IsNullOrEmpty(limit.DimensionName))
        {
            request.Dimensions = new List<Dimension>
            {
                new Dimension
                {
                    Name = limit.DimensionName,
                    Value = limit.DimensionValue
                }
            };
        }

        var response = await _cloudWatch.GetMetricStatisticsAsync(request, cancellationToken);

        // Sum all datapoints (there should be at most one with a single-period query)
        return response.Datapoints?.Sum(dp => dp.Sum) ?? 0.0;
    }
}
