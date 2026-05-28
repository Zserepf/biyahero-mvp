using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// PostgreSQL repository for the audit_log table using Dapper.
/// Extends BasePostgresRepository for generic CRUD and adds
/// audit-specific queries for actor, target, and time-range lookups.
/// Requirements: 5.10, 8.5
/// </summary>
public class AuditLogRepository : BasePostgresRepository<AuditLogEntry>, IAuditLogRepository
{
    public AuditLogRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    // ─── Table Configuration ──────────────────────────────────────────────

    protected override string TableName => "audit_log";

    // ─── Mapping ──────────────────────────────────────────────────────────

    protected override AuditLogEntry MapToEntity(dynamic row)
    {
        return new AuditLogEntry(
            id: (Guid)row.id,
            createdAt: (DateTime)row.occurred_at,
            updatedAt: (DateTime)row.occurred_at,
            actorId: (Guid)row.actor_id,
            action: (string)row.action,
            targetType: (string)row.target_type,
            targetId: row.target_id as Guid?,
            occurredAt: (DateTime)row.occurred_at,
            metadata: row.metadata as string
        );
    }

    // ─── INSERT ───────────────────────────────────────────────────────────

    protected override string GetInsertSql()
    {
        return """
            INSERT INTO audit_log (id, actor_id, action, target_type, target_id, occurred_at, metadata)
            VALUES (@Id, @ActorId, @Action, @TargetType, @TargetId, @OccurredAt, @Metadata::jsonb)
            """;
    }

    protected override object GetInsertParameters(AuditLogEntry entity)
    {
        return new
        {
            entity.Id,
            entity.ActorId,
            entity.Action,
            entity.TargetType,
            entity.TargetId,
            entity.OccurredAt,
            entity.Metadata
        };
    }

    // ─── UPDATE (audit log entries are immutable, but required by base) ───

    protected override string GetUpdateSql()
    {
        // Audit log entries are immutable — update is a no-op by design.
        // This satisfies the base class contract without allowing mutation.
        return """
            UPDATE audit_log
            SET metadata = @Metadata::jsonb
            WHERE id = @Id
            """;
    }

    protected override object GetUpdateParameters(AuditLogEntry entity)
    {
        return new
        {
            entity.Id,
            entity.Metadata
        };
    }

    // ─── IAuditLogRepository Methods ──────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLogEntry>> FindByActorIdAsync(Guid actorId)
    {
        const string sql = """
            SELECT id, actor_id, action, target_type, target_id, occurred_at, metadata
            FROM audit_log
            WHERE actor_id = @ActorId
            ORDER BY occurred_at DESC
            """;

        return await QueryAsync(sql, new { ActorId = actorId });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLogEntry>> FindByTargetAsync(string targetType, Guid targetId)
    {
        const string sql = """
            SELECT id, actor_id, action, target_type, target_id, occurred_at, metadata
            FROM audit_log
            WHERE target_type = @TargetType AND target_id = @TargetId
            ORDER BY occurred_at DESC
            """;

        return await QueryAsync(sql, new { TargetType = targetType, TargetId = targetId });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLogEntry>> FindByTimeRangeAsync(DateTime from, DateTime to)
    {
        const string sql = """
            SELECT id, actor_id, action, target_type, target_id, occurred_at, metadata
            FROM audit_log
            WHERE occurred_at >= @From AND occurred_at <= @To
            ORDER BY occurred_at DESC
            """;

        return await QueryAsync(sql, new { From = from, To = to });
    }
}
