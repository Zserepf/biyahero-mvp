using BiyaHero.Api.Features.Fare;

namespace BiyaHero.Api.Tests.Features.Fare;

/// <summary>
/// Unit tests for the FareMatrixLoader.
/// Validates: Requirements 2.1, 2.8
/// </summary>
public class FareMatrixLoaderTests
{
    // ─── JSON Config Fallback Tests ──────────────────────────────────────

    [Fact]
    public async Task GetActiveMatrixAsync_WhenDatabaseEmpty_LoadsFromJsonConfig()
    {
        // Arrange: empty repository + valid JSON config
        var repository = new EmptyFareMatrixRepository();
        var configPath = CreateTempFareMatrixJson();

        try
        {
            var loader = new FareMatrixLoader(repository, jsonConfigPath: configPath);

            // Act
            var matrix = await loader.GetActiveMatrixAsync("jeepney");

            // Assert
            Assert.NotNull(matrix);
            Assert.Equal("jeepney", matrix.VehicleType);
            Assert.Equal(1300, matrix.MinFareCentavos);
            Assert.Equal(4.0, matrix.MinFareKm);
            Assert.Equal(180, matrix.PerKmCentavos);
            Assert.Equal("v1-test", matrix.Version);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task GetAllActiveMatricesAsync_WhenDatabaseEmpty_LoadsAllVehicleTypes()
    {
        // Arrange
        var repository = new EmptyFareMatrixRepository();
        var configPath = CreateTempFareMatrixJson();

        try
        {
            var loader = new FareMatrixLoader(repository, jsonConfigPath: configPath);

            // Act
            var matrices = await loader.GetAllActiveMatricesAsync();

            // Assert
            Assert.Equal(4, matrices.Count);
            Assert.Contains(matrices, m => m.VehicleType == "jeepney");
            Assert.Contains(matrices, m => m.VehicleType == "bus");
            Assert.Contains(matrices, m => m.VehicleType == "uv_express");
            Assert.Contains(matrices, m => m.VehicleType == "tricycle");
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task GetActiveMatrixAsync_VehicleTypeLookup_IsCaseInsensitive()
    {
        // Arrange
        var repository = new EmptyFareMatrixRepository();
        var configPath = CreateTempFareMatrixJson();

        try
        {
            var loader = new FareMatrixLoader(repository, jsonConfigPath: configPath);

            // Act
            var matrix = await loader.GetActiveMatrixAsync("JEEPNEY");

            // Assert
            Assert.NotNull(matrix);
            Assert.Equal("jeepney", matrix.VehicleType);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task GetActiveMatrixAsync_UnsupportedVehicleType_ReturnsNull()
    {
        // Arrange
        var repository = new EmptyFareMatrixRepository();
        var configPath = CreateTempFareMatrixJson();

        try
        {
            var loader = new FareMatrixLoader(repository, jsonConfigPath: configPath);

            // Act
            var matrix = await loader.GetActiveMatrixAsync("motorcycle");

            // Assert
            Assert.Null(matrix);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    // ─── Database Priority Tests ─────────────────────────────────────────

    [Fact]
    public async Task GetActiveMatrixAsync_WhenDatabaseHasData_UsesDatabaseOverJson()
    {
        // Arrange: repository with data
        var dbMatrix = new FareMatrix(
            version: "v2-db",
            vehicleType: "jeepney",
            minFareCentavos: 1400,
            minFareKm: 4.0,
            perKmCentavos: 200,
            discountPercentByCategory: new Dictionary<string, int>
            {
                ["regular"] = 0,
                ["student"] = 20,
                ["senior"] = 20,
                ["pwd"] = 20
            });

        var repository = new InMemoryFareMatrixRepository(new[] { dbMatrix });
        var configPath = CreateTempFareMatrixJson();

        try
        {
            var loader = new FareMatrixLoader(repository, jsonConfigPath: configPath);

            // Act
            var matrix = await loader.GetActiveMatrixAsync("jeepney");

            // Assert: should use database version, not JSON
            Assert.NotNull(matrix);
            Assert.Equal("v2-db", matrix.Version);
            Assert.Equal(1400, matrix.MinFareCentavos);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    // ─── Cache Tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveMatrixAsync_CachesResults_DoesNotReloadOnSecondCall()
    {
        // Arrange
        var repository = new CountingFareMatrixRepository();
        var configPath = CreateTempFareMatrixJson();

        try
        {
            var loader = new FareMatrixLoader(repository, jsonConfigPath: configPath);

            // Act: call twice
            await loader.GetActiveMatrixAsync("jeepney");
            await loader.GetActiveMatrixAsync("bus");

            // Assert: repository was only called once (cache hit on second call)
            Assert.Equal(1, repository.CallCount);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task RefreshAsync_InvalidatesCache_ReloadsOnNextCall()
    {
        // Arrange
        var repository = new CountingFareMatrixRepository();
        var configPath = CreateTempFareMatrixJson();

        try
        {
            var loader = new FareMatrixLoader(repository, jsonConfigPath: configPath);

            // Load initial cache
            await loader.GetActiveMatrixAsync("jeepney");
            Assert.Equal(1, repository.CallCount);

            // Refresh
            await loader.RefreshAsync();

            // Next call should reload
            await loader.GetActiveMatrixAsync("jeepney");
            // RefreshAsync itself loads, so total should be 2
            Assert.Equal(2, repository.CallCount);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    // ─── Discount Category Tests ─────────────────────────────────────────

    [Fact]
    public async Task LoadedMatrix_ContainsDiscountCategories()
    {
        // Arrange
        var repository = new EmptyFareMatrixRepository();
        var configPath = CreateTempFareMatrixJson();

        try
        {
            var loader = new FareMatrixLoader(repository, jsonConfigPath: configPath);

            // Act
            var matrix = await loader.GetActiveMatrixAsync("jeepney");

            // Assert
            Assert.NotNull(matrix);
            Assert.Equal(4, matrix.DiscountPercentByCategory.Count);
            Assert.Equal(0, matrix.DiscountPercentByCategory["regular"]);
            Assert.Equal(20, matrix.DiscountPercentByCategory["student"]);
            Assert.Equal(20, matrix.DiscountPercentByCategory["senior"]);
            Assert.Equal(20, matrix.DiscountPercentByCategory["pwd"]);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task GetActiveMatrixAsync_ThrowsOnNullVehicleType()
    {
        var repository = new EmptyFareMatrixRepository();
        var loader = new FareMatrixLoader(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => loader.GetActiveMatrixAsync(null!));
    }

    [Fact]
    public async Task GetActiveMatrixAsync_ThrowsOnEmptyVehicleType()
    {
        var repository = new EmptyFareMatrixRepository();
        var loader = new FareMatrixLoader(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => loader.GetActiveMatrixAsync(""));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static string CreateTempFareMatrixJson()
    {
        var path = Path.GetTempFileName();
        var json = """
        {
          "version": "v1-test",
          "effectiveDate": "2024-01-01T00:00:00Z",
          "matrices": [
            {
              "vehicleType": "jeepney",
              "minFareCentavos": 1300,
              "minFareKm": 4.0,
              "perKmCentavos": 180,
              "discountPercentByCategory": { "regular": 0, "student": 20, "senior": 20, "pwd": 20 }
            },
            {
              "vehicleType": "bus",
              "minFareCentavos": 1500,
              "minFareKm": 5.0,
              "perKmCentavos": 265,
              "discountPercentByCategory": { "regular": 0, "student": 20, "senior": 20, "pwd": 20 }
            },
            {
              "vehicleType": "uv_express",
              "minFareCentavos": 3000,
              "minFareKm": 5.0,
              "perKmCentavos": 200,
              "discountPercentByCategory": { "regular": 0, "student": 20, "senior": 20, "pwd": 20 }
            },
            {
              "vehicleType": "tricycle",
              "minFareCentavos": 4000,
              "minFareKm": 2.0,
              "perKmCentavos": 500,
              "discountPercentByCategory": { "regular": 0, "student": 20, "senior": 20, "pwd": 20 }
            }
          ]
        }
        """;
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>
    /// Repository that always returns an empty list (simulates no database data).
    /// </summary>
    private class EmptyFareMatrixRepository : IFareMatrixRepository
    {
        public Task<IReadOnlyList<FareMatrix>> GetActiveMatricesAsync()
            => Task.FromResult<IReadOnlyList<FareMatrix>>(Array.Empty<FareMatrix>());

        public Task<FareMatrix?> GetActiveMatrixByVehicleTypeAsync(string vehicleType)
            => Task.FromResult<FareMatrix?>(null);
    }

    /// <summary>
    /// Repository that returns pre-configured matrices (simulates database with data).
    /// </summary>
    private class InMemoryFareMatrixRepository : IFareMatrixRepository
    {
        private readonly IReadOnlyList<FareMatrix> _matrices;

        public InMemoryFareMatrixRepository(IEnumerable<FareMatrix> matrices)
        {
            _matrices = matrices.ToList().AsReadOnly();
        }

        public Task<IReadOnlyList<FareMatrix>> GetActiveMatricesAsync()
            => Task.FromResult(_matrices);

        public Task<FareMatrix?> GetActiveMatrixByVehicleTypeAsync(string vehicleType)
            => Task.FromResult(_matrices.FirstOrDefault(m =>
                string.Equals(m.VehicleType, vehicleType, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Repository that counts how many times it was called (for cache verification).
    /// Returns empty results so the JSON fallback is used.
    /// </summary>
    private class CountingFareMatrixRepository : IFareMatrixRepository
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<FareMatrix>> GetActiveMatricesAsync()
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<FareMatrix>>(Array.Empty<FareMatrix>());
        }

        public Task<FareMatrix?> GetActiveMatrixByVehicleTypeAsync(string vehicleType)
            => Task.FromResult<FareMatrix?>(null);
    }
}
