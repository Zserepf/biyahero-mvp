namespace BiyaHero.Api.Features.Fare;

/// <summary>
/// Result of a fare calculation. Contains the fare in PHP, distance in km,
/// and the matrix version used.
/// </summary>
public sealed class FareResult
{
    /// <summary>
    /// Computed fare in Philippine Peso (e.g., 13.00).
    /// </summary>
    public decimal AmountPhp { get; }

    /// <summary>
    /// Great-circle distance between origin and destination in kilometers.
    /// </summary>
    public double DistanceKm { get; }

    /// <summary>
    /// Version of the LTFRB fare matrix used for this calculation.
    /// </summary>
    public string MatrixVersion { get; }

    public FareResult(decimal amountPhp, double distanceKm, string matrixVersion)
    {
        AmountPhp = amountPhp;
        DistanceKm = distanceKm;
        MatrixVersion = matrixVersion;
    }
}

/// <summary>
/// Result of a distance-based fare calculation with full breakdown.
/// Contains the discounted fare, original fare, discount status, vehicle type,
/// distance, and passenger category.
/// 
/// Requirements: 2.2, 2.3, 2.4, 2.5, 2.10
/// </summary>
public sealed record CalculateResult
{
    /// <summary>
    /// Final fare in centavos after discount is applied and rounded to the nearest centavo.
    /// </summary>
    public int FareCentavos { get; init; }

    /// <summary>
    /// Whether a discount was applied (true if discount percentage > 0).
    /// </summary>
    public bool DiscountApplied { get; init; }

    /// <summary>
    /// Original fare in centavos before any discount was applied.
    /// </summary>
    public int OriginalFareCentavos { get; init; }

    /// <summary>
    /// The vehicle type from the fare matrix used for this calculation.
    /// </summary>
    public string VehicleType { get; init; } = string.Empty;

    /// <summary>
    /// The distance in kilometers used for the fare calculation.
    /// </summary>
    public double DistanceKm { get; init; }

    /// <summary>
    /// The passenger category used for discount lookup (e.g., "regular", "student", "senior", "pwd").
    /// </summary>
    public string Category { get; init; } = string.Empty;
}

/// <summary>
/// Pure domain class that computes legally correct fares using the LTFRB fare matrix.
/// 
/// Algorithm:
///   1. Compute great-circle distance using Haversine on WGS84 (Earth radius = 6371 km)
///   2. Look up the fare matrix for the given vehicle type
///   3. If distance &lt;= min_fare_km → raw fare = min_fare_centavos
///      Else → raw fare = min_fare_centavos + (distance - min_fare_km) * per_km_centavos
///   4. Apply discount percentage: discounted = raw * (1 - discount_percent / 100)
///   5. Round to nearest 25 centavos
///   6. Convert centavos to PHP (divide by 100)
///
/// This class is a pure function: given the same inputs and the same loaded matrix,
/// it always produces the same output. No I/O occurs after the matrix is loaded.
/// 
/// Requirements: 2.2, 2.3, 2.4, 2.5, 2.10
/// </summary>
public static class FareCalculator
{
    /// <summary>
    /// Earth's mean radius in kilometers (WGS84 approximation).
    /// </summary>
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// Calculate the fare for a given distance and passenger category using the provided fare matrix.
    /// This is a pure function: same inputs always produce the same output (deterministic).
    /// 
    /// Algorithm:
    ///   1. If distance &lt;= MinFareKm → originalFare = MinFareCentavos
    ///      Else → originalFare = MinFareCentavos + (distance - MinFareKm) * PerKmCentavos
    ///   2. Look up discount percentage for the category
    ///   3. discountedFare = originalFare * (1 - discountPercent / 100)
    ///   4. Round to nearest centavo (integer)
    /// 
    /// Requirements: 2.2, 2.3, 2.4, 2.5, 2.10
    /// </summary>
    /// <param name="matrix">The fare matrix containing pricing rules for the vehicle type.</param>
    /// <param name="distanceKm">The distance in kilometers.</param>
    /// <param name="category">
    /// Passenger category for discount lookup (e.g., "regular", "student", "senior", "pwd").
    /// If null or empty, defaults to "regular" (0% discount).
    /// </param>
    /// <returns>A CalculateResult with full fare breakdown.</returns>
    public static CalculateResult CalculateFare(FareMatrix matrix, double distanceKm, string? category = null)
    {
        // Step 1: Compute original fare in centavos
        int originalFareCentavos;
        if (distanceKm <= matrix.MinFareKm)
        {
            originalFareCentavos = matrix.MinFareCentavos;
        }
        else
        {
            double excessKm = distanceKm - matrix.MinFareKm;
            double increment = excessKm * matrix.PerKmCentavos;
            originalFareCentavos = matrix.MinFareCentavos + (int)Math.Round(increment, MidpointRounding.AwayFromZero);
        }

        // Step 2: Get discount percentage
        int discountPercent = GetDiscountPercent(matrix, category);

        // Step 3: Apply discount
        double discountedFare = originalFareCentavos * (1.0 - discountPercent / 100.0);

        // Step 4: Round to nearest centavo (integer)
        int fareCentavos = (int)Math.Round(discountedFare, MidpointRounding.AwayFromZero);

        // Normalize category for the result
        string normalizedCategory = string.IsNullOrWhiteSpace(category)
            ? "regular"
            : category.Trim().ToLowerInvariant();

        return new CalculateResult
        {
            FareCentavos = fareCentavos,
            DiscountApplied = discountPercent > 0,
            OriginalFareCentavos = originalFareCentavos,
            VehicleType = matrix.VehicleType,
            DistanceKm = distanceKm,
            Category = normalizedCategory
        };
    }

    /// <summary>
    /// Calculate the fare between two geographic points.
    /// </summary>
    /// <param name="originLat">Origin latitude in degrees.</param>
    /// <param name="originLng">Origin longitude in degrees.</param>
    /// <param name="destinationLat">Destination latitude in degrees.</param>
    /// <param name="destinationLng">Destination longitude in degrees.</param>
    /// <param name="matrix">The fare matrix for the requested vehicle type.</param>
    /// <param name="discountCategory">
    /// Optional discount category (e.g., "regular", "student", "senior", "pwd").
    /// If null or empty, defaults to "regular" (0% discount).
    /// </param>
    /// <returns>A FareResult containing the fare in PHP, distance in km, and matrix version.</returns>
    public static FareResult Calculate(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        FareMatrix matrix,
        string? discountCategory = null)
    {
        // Step 1: Compute Haversine distance
        double distanceKm = HaversineDistanceKm(originLat, originLng, destinationLat, destinationLng);

        // Step 2: Compute raw fare in centavos
        int rawFareCentavos = ComputeRawFareCentavos(distanceKm, matrix);

        // Step 3: Apply discount
        int discountPercent = GetDiscountPercent(matrix, discountCategory);
        double discountedCentavos = rawFareCentavos * (1.0 - discountPercent / 100.0);

        // Step 4: Round to nearest 25 centavos
        int roundedCentavos = RoundToNearest25Centavos(discountedCentavos);

        // Step 5: Convert to PHP
        decimal amountPhp = roundedCentavos / 100m;

        return new FareResult(amountPhp, distanceKm, matrix.Version);
    }

    /// <summary>
    /// Compute the great-circle distance between two points using the Haversine formula
    /// on the WGS84 reference ellipsoid (approximated as a sphere with radius 6371 km).
    /// 
    /// Requirement 2.2: Haversine on WGS84.
    /// </summary>
    public static double HaversineDistanceKm(
        double lat1Deg, double lng1Deg,
        double lat2Deg, double lng2Deg)
    {
        double lat1 = DegreesToRadians(lat1Deg);
        double lat2 = DegreesToRadians(lat2Deg);
        double dLat = DegreesToRadians(lat2Deg - lat1Deg);
        double dLng = DegreesToRadians(lng2Deg - lng1Deg);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1) * Math.Cos(lat2)
                 * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    /// <summary>
    /// Compute the raw fare in centavos before discount and rounding.
    /// 
    /// Requirement 2.3: If distance &lt;= min_fare_km → return min_fare.
    /// Requirement 2.4: If distance &gt; min_fare_km → min_fare + (distance - min_fare_km) * per_km_centavos.
    /// </summary>
    public static int ComputeRawFareCentavos(double distanceKm, FareMatrix matrix)
    {
        if (distanceKm <= matrix.MinFareKm)
        {
            return matrix.MinFareCentavos;
        }

        double excessKm = distanceKm - matrix.MinFareKm;
        double incrementCentavos = excessKm * matrix.PerKmCentavos;

        return matrix.MinFareCentavos + (int)Math.Round(incrementCentavos, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Get the discount percentage for the given category from the matrix.
    /// Returns 0 if the category is null, empty, or not found (defaults to "regular").
    /// 
    /// Requirement 2.5: Apply discount percentage before rounding.
    /// </summary>
    public static int GetDiscountPercent(FareMatrix matrix, string? discountCategory)
    {
        if (string.IsNullOrWhiteSpace(discountCategory))
        {
            return 0;
        }

        string normalizedCategory = discountCategory.Trim().ToLowerInvariant();

        if (matrix.DiscountPercentByCategory.TryGetValue(normalizedCategory, out int percent))
        {
            return percent;
        }

        // Unknown category defaults to no discount (regular)
        return 0;
    }

    /// <summary>
    /// Round a centavo amount to the nearest 25 centavos.
    /// 
    /// Examples:
    ///   1312 → 1300 (nearest 25 below midpoint)
    ///   1313 → 1325 (nearest 25 above midpoint)
    ///   1325 → 1325 (already on boundary)
    ///   1337 → 1325 (nearest 25 below midpoint)
    ///   1338 → 1350 (nearest 25 above midpoint)
    /// 
    /// Requirement 2.4: Rounded to the nearest 25 centavos.
    /// </summary>
    public static int RoundToNearest25Centavos(double centavos)
    {
        // Round to nearest multiple of 25
        return (int)(Math.Round(centavos / 25.0, MidpointRounding.AwayFromZero) * 25);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
