namespace BiyaHero.Api.Repositories;

/// <summary>
/// Extension methods for registering repository infrastructure in the DI container.
/// </summary>
public static class RepositoryServiceExtensions
{
    /// <summary>
    /// Registers the PostgreSQL connection factory and base repository infrastructure.
    /// Entity-specific repositories should be registered separately in their feature modules.
    /// </summary>
    public static IServiceCollection AddPostgresRepositories(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<IDbConnectionFactory>(
            new PostgresConnectionFactory(connectionString));

        return services;
    }
}
