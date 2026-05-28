namespace BiyaHero.Api.Features.Common.FreeTierMonitor;

/// <summary>
/// Represents a single AWS free-tier service limit to monitor.
/// </summary>
public sealed record FreeTierLimit(
    string ServiceName,
    string MetricName,
    string Namespace,
    double MonthlyAllowance,
    string Unit,
    string DimensionName = "",
    string DimensionValue = "");
