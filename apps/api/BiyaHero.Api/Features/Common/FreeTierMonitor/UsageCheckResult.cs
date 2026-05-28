namespace BiyaHero.Api.Features.Common.FreeTierMonitor;

/// <summary>
/// Result of checking a single service's free-tier usage.
/// </summary>
public sealed record UsageCheckResult(
    string ServiceName,
    string MetricName,
    double MonthlyAllowance,
    double ActualUsage,
    double ActualPercentage,
    double ProjectedMonthlyUsage,
    double ProjectedPercentage,
    bool IsAlarming);
