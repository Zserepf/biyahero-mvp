using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Constructs;

namespace BiyaHero.Infra;

/// <summary>
/// CloudWatch log groups, alarms, and free-tier usage monitoring.
///
/// Creates:
/// - Audit log group (≥30-day retention) for Super Admin actions (Req 5.10)
/// - Access log group (≥30-day retention) for Payment and Auth endpoint access (Req 8.5)
/// - Free-tier usage monitor Lambda triggered on a daily schedule (Req 7.5)
/// - CloudWatch metric filter + alarm on the free-tier alert pattern
/// - SNS topic for operational alert delivery
///
/// Requirements: 5.10, 7.5, 8.5
/// </summary>
public class MonitoringStack : Stack
{
    /// <summary>The CloudWatch log group for audit log entries (Super Admin actions).</summary>
    public ILogGroup AuditLogGroup { get; }

    /// <summary>The CloudWatch log group for access logs (Payment + Auth endpoints).</summary>
    public ILogGroup AccessLogGroup { get; }

    /// <summary>The SNS topic for operational alerts (free-tier usage warnings).</summary>
    public ITopic AlertsTopic { get; }

    /// <summary>The Lambda function that checks free-tier usage on a schedule.</summary>
    public IFunction FreeTierMonitorFunction { get; }

    internal MonitoringStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // ─── CloudWatch Log Groups ────────────────────────────────────────────

        // Audit log group: immutable append-only sink for Super Admin write/delete actions.
        // Retention ≥ 30 days per Req 5.10.
        AuditLogGroup = new LogGroup(this, "AuditLogGroup", new LogGroupProps
        {
            LogGroupName = "/biyahero/audit-log",
            Retention = RetentionDays.ONE_MONTH,
            RemovalPolicy = RemovalPolicy.RETAIN
        });

        // Access log group: records access to Payment_Event and Auth_Service endpoints
        // with caller identity, endpoint, timestamp, and outcome.
        // Retention ≥ 30 days per Req 8.5.
        AccessLogGroup = new LogGroup(this, "AccessLogGroup", new LogGroupProps
        {
            LogGroupName = "/biyahero/access-log",
            Retention = RetentionDays.ONE_MONTH,
            RemovalPolicy = RemovalPolicy.RETAIN
        });

        // ─── SNS Topic for Operational Alerts ─────────────────────────────────

        // Alert delivery channel for free-tier usage warnings (Req 7.5).
        // Operators subscribe via email, Slack webhook, or other SNS-supported protocols.
        AlertsTopic = new Topic(this, "FreeTierAlertsTopic", new TopicProps
        {
            TopicName = "biyahero-free-tier-alerts",
            DisplayName = "BiyaHero Free-Tier Usage Alerts"
        });

        // ─── Free-Tier Monitor Lambda ─────────────────────────────────────────

        // Lambda function that runs the FreeTierMonitorHandler on a daily schedule.
        // It queries CloudWatch metrics for each monitored service and emits
        // log warnings when projected usage exceeds 80% AND actual ≥ 50% (Req 7.5).
        FreeTierMonitorFunction = new Function(this, "FreeTierMonitorFn", new FunctionProps
        {
            FunctionName = "biyahero-free-tier-monitor",
            Description = "Checks AWS free-tier usage and emits alerts when thresholds are breached (Req 7.5)",
            Runtime = Runtime.DOTNET_8,
            Architecture = Architecture.ARM_64,
            Handler = "BiyaHero.Api::BiyaHero.Api.Features.Common.FreeTierMonitor.FreeTierMonitorHandler::HandleAsync",
            Code = Code.FromAsset("../apps/api/BiyaHero.Api/bin/Release/net8.0/linux-arm64/publish"),
            MemorySize = 256,
            Timeout = Duration.Seconds(60),
            Environment = new Dictionary<string, string>
            {
                ["DOTNET_ENVIRONMENT"] = "Production"
            },
            LogRetention = RetentionDays.TWO_WEEKS
        });

        // Grant the Lambda permission to read CloudWatch metrics
        FreeTierMonitorFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[]
            {
                "cloudwatch:GetMetricStatistics",
                "cloudwatch:ListMetrics",
                "cloudwatch:GetMetricData"
            },
            Resources = new[] { "*" }
        }));

        // ─── EventBridge Schedule (Daily Trigger) ─────────────────────────────

        // Run the free-tier monitor once per day at 06:00 UTC.
        var dailyRule = new Rule(this, "FreeTierMonitorSchedule", new RuleProps
        {
            RuleName = "biyahero-free-tier-monitor-daily",
            Description = "Triggers the free-tier usage monitor Lambda daily at 06:00 UTC",
            Schedule = Schedule.Cron(new CronOptions
            {
                Hour = "6",
                Minute = "0"
            })
        });

        dailyRule.AddTarget(new LambdaFunction(FreeTierMonitorFunction));

        // ─── Metric Filter + Alarm on FREE-TIER ALERT Pattern ─────────────────

        // The FreeTierMonitorHandler emits log lines containing "FREE-TIER ALERT"
        // when thresholds are breached. A metric filter detects this pattern and
        // a CloudWatch alarm fires when the count is ≥ 1 in any evaluation period.
        var freeTierAlertMetric = new MetricFilter(this, "FreeTierAlertMetricFilter", new MetricFilterProps
        {
            LogGroup = (LogGroup)FreeTierMonitorFunction.LogGroup,
            FilterPattern = FilterPattern.Literal("FREE-TIER ALERT"),
            MetricNamespace = "BiyaHero/FreeTier",
            MetricName = "AlertCount",
            MetricValue = "1",
            DefaultValue = 0
        });

        var freeTierAlarm = new Alarm(this, "FreeTierUsageAlarm", new AlarmProps
        {
            AlarmName = "biyahero-free-tier-usage-breach",
            AlarmDescription = "Fires when the free-tier monitor detects projected usage > 80% AND actual >= 50% for any AWS service (Req 7.5)",
            Metric = new Metric(new MetricProps
            {
                Namespace = "BiyaHero/FreeTier",
                MetricName = "AlertCount",
                Statistic = "Sum",
                Period = Duration.Hours(24)
            }),
            Threshold = 1,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });

        // Route alarm notifications to the SNS topic
        freeTierAlarm.AddAlarmAction(new SnsAction(AlertsTopic));
        freeTierAlarm.AddOkAction(new SnsAction(AlertsTopic));

        // ─── Lambda Error Alarm ───────────────────────────────────────────────

        // Alert if the free-tier monitor Lambda itself fails (invocation errors).
        var lambdaErrorAlarm = new Alarm(this, "FreeTierMonitorErrorAlarm", new AlarmProps
        {
            AlarmName = "biyahero-free-tier-monitor-errors",
            AlarmDescription = "Fires when the free-tier monitor Lambda encounters invocation errors",
            Metric = FreeTierMonitorFunction.MetricErrors(new MetricOptions
            {
                Statistic = "Sum",
                Period = Duration.Hours(24)
            }),
            Threshold = 1,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });

        lambdaErrorAlarm.AddAlarmAction(new SnsAction(AlertsTopic));

        // ─── Stack Outputs ────────────────────────────────────────────────────

        _ = new CfnOutput(this, "AuditLogGroupName", new CfnOutputProps
        {
            Value = AuditLogGroup.LogGroupName,
            Description = "CloudWatch log group for audit log entries (30-day retention)"
        });

        _ = new CfnOutput(this, "AccessLogGroupName", new CfnOutputProps
        {
            Value = AccessLogGroup.LogGroupName,
            Description = "CloudWatch log group for access logs (30-day retention)"
        });

        _ = new CfnOutput(this, "AlertsTopicArn", new CfnOutputProps
        {
            Value = AlertsTopic.TopicArn,
            Description = "SNS topic ARN for free-tier usage alerts"
        });

        _ = new CfnOutput(this, "FreeTierMonitorFunctionArn", new CfnOutputProps
        {
            Value = FreeTierMonitorFunction.FunctionArn,
            Description = "ARN of the free-tier usage monitor Lambda"
        });
    }
}
