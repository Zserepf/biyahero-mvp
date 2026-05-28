using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BiyaHero.Api.Domain;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// DynamoDB repository for DemandPing entities.
/// 
/// Key schema:
///   PK: GEOHASH#{geohash5}  — coarse spatial bucket (~5 km cell)
///   SK: PING#{commuterId}#{pingId}
///   GSI byCommuterId: PK = commuterId (string), SK = pk (for fast cancel lookup)
///   TTL: expiresAt (epoch seconds, 5-minute TTL from submission)
///
/// Requirements: 4.1, 4.4, 4.5
/// </summary>
public class DemandPingRepository : BaseDynamoRepository<DemandPing>, IDemandPingRepository
{
    private const string GsiByCommuterId = "byCommuterId";

    public DemandPingRepository(IAmazonDynamoDB client) : base(client)
    {
    }

    // ─── Abstract Member Implementations ──────────────────────────────────

    protected override string TableName => "DemandPings";

    protected override string PartitionKeyName => "pk";

    protected override string SortKeyName => "sk";

    protected override string GetPartitionKey(DemandPing entity)
    {
        return $"GEOHASH#{entity.Geohash5}";
    }

    protected override string GetSortKey(DemandPing entity)
    {
        return $"PING#{entity.CommuterId}#{entity.Id}";
    }

    protected override Dictionary<string, AttributeValue> ToAttributeMap(DemandPing entity)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = GetPartitionKey(entity) },
            ["sk"] = new AttributeValue { S = GetSortKey(entity) },
            ["id"] = new AttributeValue { S = entity.Id.ToString() },
            ["commuterId"] = new AttributeValue { S = entity.CommuterId.ToString() },
            ["lat"] = new AttributeValue { N = entity.Latitude.ToString() },
            ["lng"] = new AttributeValue { N = entity.Longitude.ToString() },
            ["geohash5"] = new AttributeValue { S = entity.Geohash5 },
            ["geohash7"] = new AttributeValue { S = entity.Geohash7 },
            ["vehicleType"] = new AttributeValue { S = entity.VehicleType.ToString() },
            ["createdAt"] = new AttributeValue { S = entity.CreatedAt.ToString("o") },
            ["updatedAt"] = new AttributeValue { S = entity.UpdatedAt.ToString("o") },
            ["expiresAt"] = ToEpochAttribute(entity.ExpiresAt)
        };
    }

    protected override DemandPing FromAttributeMap(Dictionary<string, AttributeValue> attributes)
    {
        return new DemandPing(
            id: Guid.Parse(attributes["id"].S),
            createdAt: DateTime.Parse(attributes["createdAt"].S),
            updatedAt: DateTime.Parse(attributes["updatedAt"].S),
            commuterId: Guid.Parse(attributes["commuterId"].S),
            latitude: double.Parse(attributes["lat"].N),
            longitude: double.Parse(attributes["lng"].N),
            geohash5: attributes["geohash5"].S,
            geohash7: attributes["geohash7"].S,
            vehicleType: Enum.Parse<VehicleType>(attributes["vehicleType"].S),
            expiresAt: FromEpochAttribute(attributes["expiresAt"])
        );
    }

    /// <summary>
    /// Override to provide GSI key attribute names for the byCommuterId index.
    /// GSI PK is "commuterId", GSI SK is "pk" (the table partition key).
    /// </summary>
    protected override (string PkName, string SkName) GetIndexKeyNames(string indexName)
    {
        if (indexName == GsiByCommuterId)
        {
            return ("commuterId", "pk");
        }

        return base.GetIndexKeyNames(indexName);
    }

    // ─── IDemandPingRepository Implementation ─────────────────────────────

    /// <inheritdoc />
    public async Task<bool> PutPingAsync(DemandPing ping, CancellationToken cancellationToken = default)
    {
        return await PutItemAsync(ping, conditionalOnNotExists: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DemandPing>> GetActivePingsByGeohash5Async(
        string geohash5,
        CancellationToken cancellationToken = default)
    {
        var pk = $"GEOHASH#{geohash5}";
        var allPings = await QueryAsync(pk, skPrefix: "PING#", cancellationToken: cancellationToken);

        var now = DateTime.UtcNow;
        return allPings.Where(p => p.ExpiresAt > now).ToList();
    }

    /// <inheritdoc />
    public async Task<DemandPing?> GetActivePingByCommuterAsync(
        Guid commuterId,
        CancellationToken cancellationToken = default)
    {
        var pings = await QueryByIndexAsync(
            indexName: GsiByCommuterId,
            indexPk: commuterId.ToString(),
            cancellationToken: cancellationToken);

        var now = DateTime.UtcNow;
        return pings.FirstOrDefault(p => p.ExpiresAt > now);
    }

    /// <inheritdoc />
    public async Task DeletePingAsync(
        Guid commuterId,
        Guid pingId,
        string geohash5,
        CancellationToken cancellationToken = default)
    {
        var pk = $"GEOHASH#{geohash5}";
        var sk = $"PING#{commuterId}#{pingId}";
        await DeleteAsync(pk, sk, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DemandPing>> QueryByBboxAsync(
        IEnumerable<string> geohash5Cells,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var results = new List<DemandPing>();

        // Query each geohash5 partition in parallel for efficient bbox aggregation.
        var tasks = geohash5Cells.Select(async cell =>
        {
            var pk = $"GEOHASH#{cell}";
            return await QueryAsync(pk, skPrefix: "PING#", cancellationToken: cancellationToken);
        });

        var partitionResults = await Task.WhenAll(tasks);

        foreach (var pings in partitionResults)
        {
            results.AddRange(pings.Where(p => p.ExpiresAt > now));
        }

        return results;
    }
}
