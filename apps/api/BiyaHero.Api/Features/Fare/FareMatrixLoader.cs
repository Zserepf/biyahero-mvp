using System.Text.Json;

namespace BiyaHero.Api.Features.Fare;

/// <summary>
/// In-process cached loader for the active LTFRB fare matrix.
/// 
/// Loading strategy (in priority order):
///   1. PostgreSQL fare_matrices table (via IFareMatrixRepository) — primary source.
///   2. JSON config file (fare-matrix.json) — fallback when the database is unavailable
///      or has no data. This allows the system to function without a database migration
///      and supports updating fares by editing the JSON file without redeployment.
///
/// Caching strategy:
///   - Matrices are loaded on first access or after cache expiry.
///   - Cache expires after a configurable duration (default 5 minutes).
///   - A manual RefreshAsync() call invalidates the cache immediately,
///     allowing fare updates without redeployment (Req 2.8).
///   - Thread-safe via SemaphoreSlim to prevent thundering herd on cache miss.
/// </summary>
public class FareMatrixLoader : IFareMatrixLoader
{
    private readonly IFareMatrixRepository _repository;
    private readonly TimeSpan _cacheDuration;
    private readonly string? _jsonConfigPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private Dictionary<string, FareMatrix> _cache = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _cacheLoadedAt = DateTime.MinValue;

    /// <summary>
    /// Creates a new FareMatrixLoader with the specified repository and cache duration.
    /// </summary>
    /// <param name="repository">The repository to load fare matrices from.</param>
    /// <param name="cacheDuration">
    /// How long the cache remains valid before auto-refreshing.
    /// Pass null to use the default of 5 minutes.
    /// </param>
    /// <param name="jsonConfigPath">
    /// Optional path to a fare-matrix.json config file used as a fallback
    /// when the database has no data. Pass null to use the default path.
    /// </param>
    public FareMatrixLoader(
        IFareMatrixRepository repository,
        TimeSpan? cacheDuration = null,
        string? jsonConfigPath = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cacheDuration = cacheDuration ?? TimeSpan.FromMinutes(5);
        _jsonConfigPath = jsonConfigPath;
    }

    /// <inheritdoc />
    public async Task<FareMatrix?> GetActiveMatrixAsync(string vehicleType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vehicleType);

        await EnsureCacheLoadedAsync();

        _cache.TryGetValue(vehicleType, out var matrix);
        return matrix;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FareMatrix>> GetAllActiveMatricesAsync()
    {
        await EnsureCacheLoadedAsync();

        return _cache.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task RefreshAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await LoadCacheAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureCacheLoadedAsync()
    {
        if (IsCacheValid())
            return;

        await _lock.WaitAsync();
        try
        {
            // Double-check after acquiring the lock (another thread may have refreshed)
            if (IsCacheValid())
                return;

            await LoadCacheAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsCacheValid()
    {
        return _cache.Count > 0 && (DateTime.UtcNow - _cacheLoadedAt) < _cacheDuration;
    }

    /// <summary>
    /// Loads fare matrices from the database first. If the database returns no data,
    /// falls back to loading from the JSON config file.
    /// </summary>
    private async Task LoadCacheAsync()
    {
        var newCache = new Dictionary<string, FareMatrix>(StringComparer.OrdinalIgnoreCase);

        // Try loading from the database first
        try
        {
            var matrices = await _repository.GetActiveMatricesAsync();
            if (matrices.Count > 0)
            {
                foreach (var matrix in matrices)
                {
                    newCache[matrix.VehicleType] = matrix;
                }

                _cache = newCache;
                _cacheLoadedAt = DateTime.UtcNow;
                return;
            }
        }
        catch
        {
            // Database unavailable — fall through to JSON config fallback
        }

        // Fallback: load from JSON config file
        var jsonMatrices = LoadFromJsonConfig();
        foreach (var matrix in jsonMatrices)
        {
            newCache[matrix.VehicleType] = matrix;
        }

        _cache = newCache;
        _cacheLoadedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Loads fare matrices from the fare-matrix.json config file.
    /// This is the fallback source when the database is unavailable or empty.
    /// </summary>
    private IReadOnlyList<FareMatrix> LoadFromJsonConfig()
    {
        var configPath = ResolveJsonConfigPath();

        if (!File.Exists(configPath))
        {
            return Array.Empty<FareMatrix>();
        }

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize(json, FareMatrixJsonContext.Default.FareMatrixConfig);

        if (config is null || config.Matrices.Count == 0)
        {
            return Array.Empty<FareMatrix>();
        }

        var effectiveAt = DateTime.TryParse(config.EffectiveDate, out var parsed)
            ? parsed.ToUniversalTime()
            : DateTime.UtcNow;

        return config.Matrices.Select(entry => new FareMatrix(
            version: config.Version,
            vehicleType: entry.VehicleType,
            minFareCentavos: entry.MinFareCentavos,
            minFareKm: entry.MinFareKm,
            perKmCentavos: entry.PerKmCentavos,
            discountPercentByCategory: entry.DiscountPercentByCategory
        )
        {
            EffectiveAt = effectiveAt
        }).ToList();
    }

    /// <summary>
    /// Resolves the path to the fare-matrix.json config file.
    /// Checks the explicitly provided path first, then the app's base directory.
    /// </summary>
    private string ResolveJsonConfigPath()
    {
        if (!string.IsNullOrEmpty(_jsonConfigPath))
        {
            return _jsonConfigPath;
        }

        // Default: look in the application's base directory
        return Path.Combine(AppContext.BaseDirectory, "fare-matrix.json");
    }
}
