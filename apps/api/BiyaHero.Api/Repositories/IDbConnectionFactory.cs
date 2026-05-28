using System.Data;

namespace BiyaHero.Api.Repositories;

/// <summary>
/// Factory for creating database connections.
/// Abstracts the connection creation so repositories never deal with connection strings directly.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates and returns an open database connection.
    /// </summary>
    Task<IDbConnection> CreateConnectionAsync();
}
