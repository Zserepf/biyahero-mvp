namespace BiyaHero.Api.Services;

/// <summary>
/// Service interface for recording audit log entries.
/// Persists to the audit_log Postgres table AND mirrors to ILogger
/// (which routes to CloudWatch in production with 30-day retention).
/// Requirements: 5.10, 8.5
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Records an audit log entry for an action performed by an actor on a target.
    /// The entry is persisted to the audit_log table and mirrored to CloudWatch via ILogger.
    /// </summary>
    /// <param name="actorId">The user ID of the actor performing the action.</param>
    /// <param name="action">The action performed (e.g., "user.suspended", "user.promoted").</param>
    /// <param name="targetType">The type of the target resource (e.g., "user", "route").</param>
    /// <param name="targetId">The ID of the target resource, if applicable.</param>
    /// <param name="metadata">Optional JSON metadata providing additional context.</param>
    Task LogAsync(Guid actorId, string action, string targetType, Guid? targetId = null, string? metadata = null);

    /// <summary>
    /// Records an audit log entry with an explicit timestamp.
    /// Used when the event timestamp differs from the current time (e.g., replaying events).
    /// </summary>
    /// <param name="actorId">The user ID of the actor performing the action.</param>
    /// <param name="action">The action performed.</param>
    /// <param name="targetType">The type of the target resource.</param>
    /// <param name="targetId">The ID of the target resource, if applicable.</param>
    /// <param name="occurredAt">The UTC timestamp when the action occurred.</param>
    /// <param name="metadata">Optional JSON metadata providing additional context.</param>
    Task LogAsync(Guid actorId, string action, string targetType, Guid? targetId, DateTime occurredAt, string? metadata = null);
}
