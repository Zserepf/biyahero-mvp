using System.Data;
using BiyaHero.Api.Domain;
using Dapper;
using Route = BiyaHero.Api.Domain.Route;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// PostgreSQL repository for the routes and route_waypoints tables using Dapper.
/// Extends BasePostgresRepository for generic CRUD and adds spatial bounding-box
/// queries and waypoint-inclusive lookups required by the route browsing flow.
/// 
/// Routes are stored in the "routes" table; waypoints in "route_waypoints" joined on route_id.
/// Create/Update operations use a delete + re-insert pattern for waypoints within a transaction.
/// </summary>
public class RouteRepository : BasePostgresRepository<Route>, IRouteRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RouteRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // ─── Table Configuration ──────────────────────────────────────────────

    protected override string TableName => "routes";

    // ─── Mapping ──────────────────────────────────────────────────────────

    protected override Route MapToEntity(dynamic row)
    {
        return new Route(
            id: (Guid)row.id,
            createdAt: (DateTime)row.created_at,
            updatedAt: (DateTime)row.updated_at,
            name: (string)row.name,
            vehicleType: ParseVehicleType((string)row.vehicle_type),
            status: ParseRouteStatus((string)row.status),
            createdBy: (Guid)row.created_by,
            baseFare: (decimal)row.base_fare,
            waypoints: new List<Waypoint>()
        );
    }

    private static VehicleType ParseVehicleType(string value)
    {
        // uv_express → UV_Express, jeepney → Jeepney
        var pascal = System.Text.RegularExpressions.Regex.Replace(
            value, @"(^|_)([a-z])", m => m.Groups[2].Value.ToUpperInvariant());
        // Handle UV_Express special case
        pascal = pascal.Replace("Uv", "UV");
        return Enum.Parse<VehicleType>(pascal, ignoreCase: true);
    }

    private static RouteStatus ParseRouteStatus(string value)
    {
        var pascal = System.Text.RegularExpressions.Regex.Replace(
            value, @"(^|_)([a-z])", m => m.Groups[2].Value.ToUpperInvariant());
        return Enum.Parse<RouteStatus>(pascal, ignoreCase: true);
    }

    private static Waypoint MapToWaypoint(dynamic row)
    {
        return new Waypoint(
            latitude: (double)row.latitude,
            longitude: (double)row.longitude,
            sequenceOrder: (int)row.sequence_order,
            label: row.label as string
        );
    }

    // ─── INSERT ───────────────────────────────────────────────────────────

    protected override string GetInsertSql()
    {
        return """
            INSERT INTO routes (id, name, vehicle_type, status, created_by, base_fare, created_at, updated_at)
            VALUES (@Id, @Name, @VehicleType::vehicle_type, @Status::route_status, @CreatedBy, @BaseFare, @CreatedAt, @UpdatedAt)
            """;
    }

    protected override object GetInsertParameters(Route entity)
    {
        return new
        {
            entity.Id,
            entity.Name,
            VehicleType = entity.VehicleType.ToString().ToLowerInvariant(),
            Status = entity.Status.ToString().ToLowerInvariant(),
            entity.CreatedBy,
            entity.BaseFare,
            entity.CreatedAt,
            entity.UpdatedAt
        };
    }

    // ─── UPDATE ───────────────────────────────────────────────────────────

    protected override string GetUpdateSql()
    {
        return """
            UPDATE routes
            SET name = @Name,
                vehicle_type = @VehicleType::vehicle_type,
                status = @Status::route_status,
                created_by = @CreatedBy,
                base_fare = @BaseFare,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;
    }

    protected override object GetUpdateParameters(Route entity)
    {
        return new
        {
            entity.Id,
            entity.Name,
            VehicleType = entity.VehicleType.ToString().ToLowerInvariant(),
            Status = entity.Status.ToString().ToLowerInvariant(),
            entity.CreatedBy,
            entity.BaseFare,
            entity.UpdatedAt
        };
    }

    // ─── Overridden CRUD (handles waypoints in transactions) ──────────────

    /// <summary>
    /// Creates a route and its waypoints within a single transaction.
    /// </summary>
    public override async Task<Route> CreateAsync(Route entity)
    {
        await ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            // Insert the route row
            var routeSql = GetInsertSql();
            var routeParams = GetInsertParameters(entity);
            await connection.ExecuteAsync(routeSql, routeParams, transaction);

            // Insert waypoints
            await InsertWaypointsAsync(connection, transaction, entity.Id, entity.Waypoints);
        });

        return entity;
    }

    /// <summary>
    /// Updates a route and replaces its waypoints within a single transaction (delete + re-insert).
    /// </summary>
    public override async Task<Route> UpdateAsync(Route entity)
    {
        await ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            // Update the route row
            var routeSql = GetUpdateSql();
            var routeParams = GetUpdateParameters(entity);
            var affected = await connection.ExecuteAsync(routeSql, routeParams, transaction);
            if (affected == 0)
            {
                throw new InvalidOperationException($"Entity with id {entity.Id} not found in {TableName}");
            }

            // Delete existing waypoints and re-insert
            await DeleteWaypointsAsync(connection, transaction, entity.Id);
            await InsertWaypointsAsync(connection, transaction, entity.Id, entity.Waypoints);
        });

        return entity;
    }

    /// <summary>
    /// Saves (upserts) a route and its waypoints within a single transaction.
    /// </summary>
    public override async Task<Route> SaveAsync(Route entity)
    {
        await ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var existsSql = $"SELECT COUNT(1) FROM {TableName} WHERE id = @Id";
            var exists = await connection.ExecuteScalarAsync<int>(existsSql, new { entity.Id }, transaction) > 0;

            if (exists)
            {
                var updateSql = GetUpdateSql();
                var updateParams = GetUpdateParameters(entity);
                await connection.ExecuteAsync(updateSql, updateParams, transaction);
            }
            else
            {
                var insertSql = GetInsertSql();
                var insertParams = GetInsertParameters(entity);
                await connection.ExecuteAsync(insertSql, insertParams, transaction);
            }

            // Delete existing waypoints and re-insert
            await DeleteWaypointsAsync(connection, transaction, entity.Id);
            await InsertWaypointsAsync(connection, transaction, entity.Id, entity.Waypoints);
        });

        return entity;
    }

    /// <summary>
    /// Deletes a route and its waypoints within a single transaction.
    /// </summary>
    public override async Task DeleteAsync(Route entity)
    {
        await ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            // Delete waypoints first (FK constraint)
            await DeleteWaypointsAsync(connection, transaction, entity.Id);

            // Delete the route
            var sql = $"DELETE FROM {TableName} WHERE id = @Id";
            await connection.ExecuteAsync(sql, new { entity.Id }, transaction);
        });
    }

    // ─── IRouteRepository Methods ─────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<Route>> FindByBboxAsync(
        double minLat, double minLon, double maxLat, double maxLon)
    {
        // Use PostGIS ST_Intersects with the GiST-indexed location column
        // for p95 ≤ 800ms performance target (Req 1.2).
        // ST_MakeEnvelope takes (xmin, ymin, xmax, ymax, srid) where x=longitude, y=latitude.
        const string sql = """
            SELECT DISTINCT r.id, r.name, r.vehicle_type, r.status, r.created_by,
                   r.base_fare, r.created_at, r.updated_at
            FROM routes r
            INNER JOIN route_waypoints rw ON rw.route_id = r.id
            WHERE ST_Intersects(
                rw.location,
                ST_MakeEnvelope(@MinLon, @MinLat, @MaxLon, @MaxLat, 4326)
            )
            ORDER BY r.created_at DESC
            """;

        var routes = await QueryAsync(sql, new { MinLat = minLat, MinLon = minLon, MaxLat = maxLat, MaxLon = maxLon });

        // Load waypoints for each route found
        return await LoadWaypointsForRoutesAsync(routes);
    }

    /// <inheritdoc />
    public async Task<Route?> FindByIdWithWaypointsAsync(Guid id)
    {
        var route = await FindByIdAsync(id);
        if (route is null)
            return null;

        await LoadWaypointsAsync(route);
        return route;
    }

    // ─── Private Helpers ──────────────────────────────────────────────────

    private static async Task InsertWaypointsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid routeId,
        List<Waypoint> waypoints)
    {
        if (waypoints.Count == 0)
            return;

        // Insert waypoints with the PostGIS location column populated via ST_SetSRID(ST_MakePoint(lng, lat), 4326)
        // This ensures the GiST index on location is usable for bbox queries (Req 1.2).
        const string waypointSql = """
            INSERT INTO route_waypoints (route_id, latitude, longitude, sequence_order, label, location)
            VALUES (@RouteId, @Latitude, @Longitude, @SequenceOrder, @Label,
                    ST_SetSRID(ST_MakePoint(@Longitude, @Latitude), 4326))
            """;

        foreach (var waypoint in waypoints)
        {
            await connection.ExecuteAsync(waypointSql, new
            {
                RouteId = routeId,
                waypoint.Latitude,
                waypoint.Longitude,
                waypoint.SequenceOrder,
                waypoint.Label
            }, transaction);
        }
    }

    private static async Task DeleteWaypointsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid routeId)
    {
        const string sql = "DELETE FROM route_waypoints WHERE route_id = @RouteId";
        await connection.ExecuteAsync(sql, new { RouteId = routeId }, transaction);
    }

    private async Task LoadWaypointsAsync(Route route)
    {
        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;

        const string sql = """
            SELECT latitude, longitude, sequence_order, label
            FROM route_waypoints
            WHERE route_id = @RouteId
            ORDER BY sequence_order ASC
            """;

        var rows = await dbConnection.QueryAsync(sql, new { RouteId = route.Id });
        route.Waypoints = rows.Select(row => MapToWaypoint((object)row)).ToList();
    }

    private async Task<IReadOnlyList<Route>> LoadWaypointsForRoutesAsync(IReadOnlyList<Route> routes)
    {
        if (routes.Count == 0)
            return routes;

        await using var connection = (await _connectionFactory.CreateConnectionAsync()) as IAsyncDisposable
            ?? throw new InvalidOperationException("Connection does not support async disposal");

        var dbConnection = (IDbConnection)connection;

        var routeIds = routes.Select(r => r.Id).ToArray();

        const string sql = """
            SELECT route_id, latitude, longitude, sequence_order, label
            FROM route_waypoints
            WHERE route_id = ANY(@RouteIds)
            ORDER BY route_id, sequence_order ASC
            """;

        var rows = await dbConnection.QueryAsync(sql, new { RouteIds = routeIds });

        var waypointsByRouteId = rows
            .GroupBy(row => (Guid)row.route_id)
            .ToDictionary(
                g => g.Key,
                g => g.Select(row => MapToWaypoint((object)row)).ToList()
            );

        foreach (var route in routes)
        {
            route.Waypoints = waypointsByRouteId.TryGetValue(route.Id, out var waypoints)
                ? waypoints
                : new List<Waypoint>();
        }

        return routes;
    }
}
