using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using Microsoft.Extensions.Logging;

namespace BiyaHero.Api.Services;

/// <summary>
/// Audit log service that persists entries to the audit_log Postgres table
/// AND mirrors them to ILogger (routed to CloudWatch in production) for
/// 30-day retention as an immutable append-only sink.
/// 
/// Requirements: 5.10, 8.5
/// - Req 5.10: Super Admin write/delete actions logged with actor, target, action, timestamp.
/// - Req 8.5: Access to Payment and Auth endpoints logged with caller identity, endpoint, timestamp, outcome.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _repository;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IAuditLogRepository repository, ILogger<AuditLogService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task LogAsync(Guid actorId, string action, string targetType, Guid? targetId = null, string? metadata = null)
    {
        return LogAsync(actorId, action, targetType, targetId, DateTime.UtcNow, metadata);
    }

    /// <inheritdoc />
    public async Task LogAsync(Guid actorId, string action, string targetType, Guid? targetId, DateTime occurredAt, string? metadata = null)
    {
        var entry = new AuditLogEntry
        {
            ActorId = actorId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            OccurredAt = occurredAt,
            Metadata = metadata
        };

        // Persist to the audit_log Postgres table (queryable system-of-record)
        await _repository.CreateAsync(entry);

        // Mirror to ILogger → CloudWatch log group (immutable append-only sink, 30-day retention)
        _logger.LogInformation(
            "AUDIT | Actor={ActorId} | Action={Action} | TargetType={TargetType} | TargetId={TargetId} | OccurredAt={OccurredAt} | Metadata={Metadata}",
            actorId,
            action,
            targetType,
            targetId?.ToString() ?? "none",
            occurredAt.ToString("o"),
            metadata ?? "none");
    }
}
