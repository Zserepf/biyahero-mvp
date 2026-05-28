namespace BiyaHero.Api.Features.Fare;

/// <summary>
/// Extension methods for registering Fare feature services in the DI container.
/// </summary>
public static class FareServiceExtensions
{
    /// <summary>
    /// Registers the fare matrix repository and loader as singletons.
    /// The loader is a singleton so the in-process cache is shared across all requests.
    /// The JSON config file path defaults to fare-matrix.json in the app's base directory.
    /// </summary>
    public static IServiceCollection AddFareServices(this IServiceCollection services)
    {
        services.AddSingleton<IFareMatrixRepository, FareMatrixRepository>();
        services.AddSingleton<IFareMatrixLoader>(sp =>
        {
            var repository = sp.GetRequiredService<IFareMatrixRepository>();
            return new FareMatrixLoader(repository);
        });
        return services;
    }
}
