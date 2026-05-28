using System.Data;
using BiyaHero.Api.Domain;
using Dapper;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// PostgreSQL repository for the route_votes table using Dapper.
/// Enforces one vote per user per route via the UNIQUE(route_id, voter_id) constraint.
/// </summary>
public class RouteVoteRepository : BasePostgresRepository<RouteVote>, IRouteVoteRepository
{
    public RouteVoteRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    protected override string TableName => "route_votes";

    protected override RouteVote MapToEntity(dynamic row)
    {
        return new RouteVote(
            id: (Guid)row.id,
            createdAt: (DateTime)row.created_at,
            updatedAt: (DateTime)row.updated_at,
            routeId: (Guid)row.route_id,
            voterId: (Guid)row.voter_id,
            kind: Enum.Parse<VoteKind>((string)row.kind, ignoreCase: true),
            timestamp: (DateTime)row.created_at);
    }

    protected override string GetInsertSql()
    {
        return """
            INSERT INTO route_votes (id, route_id, voter_id, kind, created_at, updated_at)
            VALUES (@Id, @RouteId, @VoterId, @Kind, @CreatedAt, @UpdatedAt)
            """;
    }

    protected override object GetInsertParameters(RouteVote entity)
    {
        return new
        {
            entity.Id,
            entity.RouteId,
            entity.VoterId,
            Kind = entity.Kind.ToString(),
            entity.CreatedAt,
            entity.UpdatedAt
        };
    }

    protected override string GetUpdateSql()
    {
        return """
            UPDATE route_votes
            SET kind = @Kind,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;
    }

    protected override object GetUpdateParameters(RouteVote entity)
    {
        return new
        {
            entity.Id,
            Kind = entity.Kind.ToString(),
            entity.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid routeId, Guid voterId)
    {
        const string sql = "SELECT COUNT(1) FROM route_votes WHERE route_id = @RouteId AND voter_id = @VoterId";
        var count = await ScalarAsync<int>(sql, new { RouteId = routeId, VoterId = voterId });
        return count > 0;
    }
}
