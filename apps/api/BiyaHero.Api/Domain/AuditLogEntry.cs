namespace BiyaHero.Api.Domain;

/// <summary>
/// Domain entity representing an immutable audit log entry.
/// Records actor, action, target, and timestamp for Super Admin,
/// Auth, and Payment operations.
/// Requirements: 5.10, 8.5
/// </summary>
public class AuditLogEntry : BaseDomain
{
    /// <summary>
    /// The user ID of the actor who performed the action.
    /// </summary>
    public Guid ActorId { get; set; }

    /// <summary>
    /// The action performed (e.g., "user.suspended", "user.promoted", "user.registered").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// The type of the target resource (e.g., "user", "route", "payment").
    /// </summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the target resource, if applicable.
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary>
    /// When the action occurred (UTC).
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// Optional JSON metadata providing additional context about the action.
    /// </summary>
    public string? Metadata { get; set; }

    public AuditLogEntry() : base()
    {
        OccurredAt = DateTime.UtcNow;
    }

    public AuditLogEntry(
        Guid id,
        DateTime createdAt,
        DateTime updatedAt,
        Guid actorId,
        string action,
        string targetType,
        Guid? targetId,
        DateTime occurredAt,
        string? metadata)
        : base(id, createdAt, updatedAt)
    {
        ActorId = actorId;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        OccurredAt = occurredAt;
        Metadata = metadata;
    }

    public override Dictionary<string, object?> Serialize()
    {
        var dict = base.Serialize();
        dict["actorId"] = ActorId.ToString();
        dict["action"] = Action;
        dict["targetType"] = TargetType;
        dict["targetId"] = TargetId?.ToString();
        dict["occurredAt"] = OccurredAt.ToString("o");
        dict["metadata"] = Metadata;
        return dict;
    }
}
