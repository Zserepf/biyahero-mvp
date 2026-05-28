using System.Data;
using Npgsql;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Creates Npgsql connections to PostgreSQL.
/// This is the only place where the connection string is referenced.
/// </summary>
public sealed class PostgresConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public PostgresConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task<IDbConnection> CreateConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
