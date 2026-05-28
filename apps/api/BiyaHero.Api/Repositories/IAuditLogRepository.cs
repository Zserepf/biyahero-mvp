using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Repository interface for audit log persistence.
/// Extends the generic IRepository with audit-specific queries.
/// Requirements: 5.10, 8.5
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLogEntry>
{
    /// <summary>
    /// Finds all audit log entries for a given actor, ordered by most recent first.
    /// </summary>
    Task<IReadOnlyList<AuditLogEntry>> FindByActorIdAsync(Guid actorId);

    /// <summary>
    /// Finds all audit log entries for a given target resource.
    /// </summary>
    Task<IReadOnlyList<AuditLogEntry>> FindByTargetAsync(string targetType, Guid targetId);

    /// <summary>
    /// Finds audit log entries within a time range, ordered by most recent first.
    /// </summary>
    Task<IReadOnlyList<AuditLogEntry>> FindByTimeRangeAsync(DateTime from, DateTime to);
}
