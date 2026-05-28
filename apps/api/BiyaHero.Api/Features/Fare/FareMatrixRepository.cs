using System.Data;
using System.Text.Json;
using BiyaHero.Api.Repositories;
using Dapper;

namespace BiyaHero.Api.Features.Fare;

/// <summary>
/// PostgreSQL repository for the fare_matrices table.
/// Loads the active (latest effective) fare matrix rows using Dapper.
/// </summary>
public class FareMatrixRepository : IFareMatrixRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public FareMatrixRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FareMatrix>> GetActiveMatricesAsync()
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;

        // For each vehicle_type, select the row with the latest effective_at that is <= now.
        // Uses DISTINCT ON (PostgreSQL) to pick one row per vehicle_type ordered by effective_at DESC.
        const string sql = """
            SELECT DISTINCT ON (vehicle_type)
                id, version, effective_at, vehicle_type,
                min_fare_centavos, min_fare_km, per_km_centavos,
                discount_percent_by_category,
                created_at, updated_at
            FROM fare_matrices
            WHERE effective_at <= NOW()
            ORDER BY vehicle_type, effective_at DESC
            """;

        var rows = await dbConnection.QueryAsync(sql);

        return rows.Select(MapToEntity).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<FareMatrix?> GetActiveMatrixByVehicleTypeAsync(string vehicleType)
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;

        const string sql = """
            SELECT id, version, effective_at, vehicle_type,
                   min_fare_centavos, min_fare_km, per_km_centavos,
                   discount_percent_by_category,
                   created_at, updated_at
            FROM fare_matrices
            WHERE vehicle_type = @VehicleType AND effective_at <= NOW()
            ORDER BY effective_at DESC
            LIMIT 1
            """;

        var row = await dbConnection.QueryFirstOrDefaultAsync(sql, new { VehicleType = vehicleType });

        return row is null ? null : MapToEntity(row);
    }

    private static FareMatrix MapToEntity(dynamic row)
    {
        var discountJson = (string?)row.discount_percent_by_category;
        var discountDict = new Dictionary<string, int>();

        if (!string.IsNullOrEmpty(discountJson))
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(discountJson);
            if (parsed is not null)
            {
                discountDict = parsed;
            }
        }

        return new FareMatrix(
            id: (Guid)row.id,
            createdAt: (DateTime)row.created_at,
            updatedAt: (DateTime)row.updated_at,
            version: (string)row.version,
            effectiveAt: (DateTime)row.effective_at,
            vehicleType: (string)row.vehicle_type,
            minFareCentavos: (int)row.min_fare_centavos,
            minFareKm: (double)row.min_fare_km,
            perKmCentavos: (int)row.per_km_centavos,
            discountPercentByCategory: discountDict
        );
    }
}
