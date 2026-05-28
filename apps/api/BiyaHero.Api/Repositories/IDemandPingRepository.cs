using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Repository interface for DemandPing-specific data access in DynamoDB.
/// Extends the generic IDynamoRepository with geohash-based queries,
/// commuter-based lookups via GSI, and batch bbox queries for heatmap aggregation.
/// Requirements: 4.1, 4.4, 4.5
/// </summary>
public interface IDemandPingRepository : IDynamoRepository<DemandPing>
{
    /// <summary>
    /// Persist a DemandPing with a conditional write (attribute_not_exists)
    /// to prevent duplicate pings for the same commuter+pingId combination.
    /// Returns true if the ping was written; false if it already exists.
    /// </summary>
    Task<bool> PutPingAsync(DemandPing ping, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query all active (non-expired) pings within a geohash5 partition.
    /// Filters out pings whose ExpiresAt is in the past.
    /// </summary>
    Task<IReadOnlyList<DemandPing>> GetActivePingsByGeohash5Async(
        string geohash5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query the GSI byCommuterId to find the active ping for a specific commuter.
    /// Used for cancel-demand operations (Req 4.5).
    /// Returns null if the commuter has no active ping.
    /// </summary>
    Task<DemandPing?> GetActivePingByCommuterAsync(
        Guid commuterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a specific ping by its composite key (geohash5, commuterId, pingId).
    /// Used for immediate cancellation (Req 4.5).
    /// </summary>
    Task DeletePingAsync(
        Guid commuterId,
        Guid pingId,
        string geohash5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch query multiple geohash5 cells for heatmap aggregation.
    /// Returns all active pings across the provided geohash5 cells,
    /// filtering out expired pings.
    /// </summary>
    Task<IReadOnlyList<DemandPing>> QueryByBboxAsync(
        IEnumerable<string> geohash5Cells,
        CancellationToken cancellationToken = default);
}
