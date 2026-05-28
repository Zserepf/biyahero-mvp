using FareMatrixModel = BiyaHero.Api.Features.Fare.FareMatrix;

namespace BiyaHero.Api.Domain;

/// <summary>
/// Detailed result of a fare calculation with full breakdown.
/// Contains baseFare, distanceCharge, discount, totalFare, and a human-readable breakdown.
/// 
/// All monetary values are in centavos (integer) for precision.
/// Convert to PHP by dividing by 100.
/// </summary>
public sealed class FareResult
{
    /// <summary>
    /// The base (minimum) fare in centavos for the first N km.
    /// </summary>
    public int BaseFareCentavos { get; }

    /// <summary>
    /// The distance charge in centavos for km beyond the minimum threshold.
    /// Zero if distance is within the minimum-fare threshold.
    /// </summary>
    public int DistanceChargeCentavos { get; }

    /// <summary>
    /// The discount amount in centavos subtracted from the subtotal.
    /// Zero for regular passengers.
    /// </summary>
    public int DiscountCentavos { get; }

    /// <summary>
    /// The final fare in centavos after discount and rounding to nearest 25 centavos.
    /// </summary>
    public int TotalFareCentavos { get; }

    /// <summary>
    /// The final fare in Philippine Peso (centavos / 100).
    /// </summary>
    public decimal TotalFarePhp => TotalFareCentavos / 100m;

    /// <summary>
    /// The computed great-circle distance in kilometers.
    /// </summary>
    public double DistanceKm { get; }

    /// <summary>
    /// The version of the LTFRB fare matrix used for this calculation.
    /// </summary>
    public string MatrixVersion { get; }

    /// <summary>
    /// The vehicle type used for this calculation.
    /// </summary>
    public string VehicleType { get; }

    /// <summary>
    /// The passenger/discount category applied (e.g., "regular", "student", "senior", "pwd").
    /// </summary>
    public string PassengerType { get; }

    /// <summary>
    /// The discount percentage applied (0 for regular, 20 for student/senior/pwd).
    /// </summary>
    public int DiscountPercent { get; }

    /// <summary>
    /// Human-readable breakdown of the fare calculation.
    /// </summary>
    public FareBreakdown Breakdown { get; }

    public FareResult(
        int baseFareCentavos,
        int distanceChargeCentavos,
        int discountCentavos,
        int totalFareCentavos,
        double distanceKm,
        string matrixVersion,
        string vehicleType,
        string passengerType,
        int discountPercent,
        FareBreakdown breakdown)
    {
        BaseFareCentavos = baseFareCentavos;
        DistanceChargeCentavos = distanceChargeCentavos;
        DiscountCentavos = discountCentavos;
        TotalFareCentavos = totalFareCentavos;
        DistanceKm = distanceKm;
        MatrixVersion = matrixVersion;
        VehicleType = vehicleType;
        PassengerType = passengerType;
        DiscountPercent = discountPercent;
        Breakdown = breakdown;
    }
}

/// <summary>
/// Human-readable breakdown of how the fare was computed.
/// Useful for displaying to the user so they can verify the calculation.
/// </summary>
public sealed class FareBreakdown
{
    /// <summary>
    /// Description of the base fare component (e.g., "First 4.0 km: ₱13.00").
    /// </summary>
    public string BaseFareDescription { get; }

    /// <summary>
    /// Description of the distance charge (e.g., "4.3 km × ₱1.80/km: ₱7.74").
    /// Empty string if distance is within the minimum threshold.
    /// </summary>
    public string DistanceChargeDescription { get; }

    /// <summary>
    /// Description of the subtotal before discount (e.g., "Subtotal: ₱20.74").
    /// </summary>
    public string SubtotalDescription { get; }

    /// <summary>
    /// Description of the discount applied (e.g., "Student discount (20%): -₱4.15").
    /// Empty string if no discount.
    /// </summary>
    public string DiscountDescription { get; }

    /// <summary>
    /// Description of the rounding step (e.g., "Rounded to nearest ₱0.25: ₱16.50").
    /// </summary>
    public string RoundingDescription { get; }

    public FareBreakdown(
        string baseFareDescription,
        string distanceChargeDescription,
        string subtotalDescription,
        string discountDescription,
        string roundingDescription)
    {
        BaseFareDescription = baseFareDescription;
        DistanceChargeDescription = distanceChargeDescription;
        SubtotalDescription = subtotalDescription;
        DiscountDescription = discountDescription;
        RoundingDescription = roundingDescription;
    }
}

/// <summary>
/// Pure domain class that computes legally correct fares using the LTFRB fare matrix.
/// 
/// This is a real-world object: a calculator that takes a FareMatrix and computes fares.
/// It encapsulates the LTFRB fare rules:
///   1. Base fare covers the first N km (vehicle-type-specific)
///   2. Per-km rate applies to distance beyond the threshold
///   3. Discount percentages for students, seniors, and PWDs (20% per LTFRB)
///   4. Final fare rounded to nearest 25 centavos
///
/// Pure domain logic — no I/O, no database calls.
/// Deterministic: same inputs always produce same output.
/// 
/// Requirements: 2.2, 2.3, 2.4, 2.5, 2.10
/// </summary>
public sealed class FareCalculator
{
    /// <summary>
    /// Earth's mean radius in kilometers (WGS84 approximation).
    /// </summary>
    private const double EarthRadiusKm = 6371.0;

    private readonly FareMatrixModel _matrix;

    /// <summary>
    /// Creates a FareCalculator bound to a specific fare matrix.
    /// All calculations use this matrix's rules.
    /// </summary>
    /// <param name="matrix">The LTFRB fare matrix for the target vehicle type.</param>
    public FareCalculator(FareMatrixModel matrix)
    {
        _matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
    }

    /// <summary>
    /// The vehicle type this calculator is configured for.
    /// </summary>
    public string VehicleType => _matrix.VehicleType;

    /// <summary>
    /// The matrix version this calculator uses.
    /// </summary>
    public string MatrixVersion => _matrix.Version;

    /// <summary>
    /// Calculate the fare between two geographic points.
    /// 
    /// Algorithm:
    ///   1. Compute great-circle distance using Haversine on WGS84
    ///   2. If distance ≤ min_fare_km → base fare only
    ///      Else → base fare + (distance - min_fare_km) × per_km_centavos
    ///   3. Apply discount percentage (before rounding)
    ///   4. Round to nearest 25 centavos
    /// </summary>
    /// <param name="originLat">Origin latitude in degrees.</param>
    /// <param name="originLng">Origin longitude in degrees.</param>
    /// <param name="destinationLat">Destination latitude in degrees.</param>
    /// <param name="destinationLng">Destination longitude in degrees.</param>
    /// <param name="passengerType">
    /// Passenger type for discount lookup: "regular", "student", "senior", "pwd".
    /// Null or empty defaults to "regular" (0% discount).
    /// </param>
    /// <returns>A FareResult with full breakdown.</returns>
    public FareResult Calculate(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        string? passengerType = null)
    {
        double distanceKm = HaversineDistanceKm(originLat, originLng, destinationLat, destinationLng);
        return CalculateFromDistance(distanceKm, passengerType);
    }

    /// <summary>
    /// Calculate the fare for a known distance (useful for testing and when distance
    /// is pre-computed from route waypoints).
    /// </summary>
    /// <param name="distanceKm">Distance in kilometers.</param>
    /// <param name="passengerType">
    /// Passenger type for discount lookup: "regular", "student", "senior", "pwd".
    /// Null or empty defaults to "regular" (0% discount).
    /// </param>
    /// <returns>A FareResult with full breakdown.</returns>
    public FareResult CalculateFromDistance(double distanceKm, string? passengerType = null)
    {
        string normalizedPassengerType = NormalizePassengerType(passengerType);

        // Step 1: Compute base fare and distance charge
        int baseFareCentavos = _matrix.MinFareCentavos;
        int distanceChargeCentavos = 0;

        if (distanceKm > _matrix.MinFareKm)
        {
            double excessKm = distanceKm - _matrix.MinFareKm;
            double rawCharge = excessKm * _matrix.PerKmCentavos;
            distanceChargeCentavos = (int)Math.Round(rawCharge, MidpointRounding.AwayFromZero);
        }

        int subtotalCentavos = baseFareCentavos + distanceChargeCentavos;

        // Step 2: Apply discount
        int discountPercent = GetDiscountPercent(normalizedPassengerType);
        double discountedAmount = subtotalCentavos * (1.0 - discountPercent / 100.0);
        int discountCentavos = subtotalCentavos - (int)Math.Round(discountedAmount, MidpointRounding.AwayFromZero);

        // Step 3: Round to nearest 25 centavos
        int totalFareCentavos = RoundToNearest25Centavos(discountedAmount);

        // Build breakdown
        var breakdown = BuildBreakdown(
            baseFareCentavos,
            distanceChargeCentavos,
            subtotalCentavos,
            discountPercent,
            normalizedPassengerType,
            discountedAmount,
            totalFareCentavos,
            distanceKm);

        return new FareResult(
            baseFareCentavos: baseFareCentavos,
            distanceChargeCentavos: distanceChargeCentavos,
            discountCentavos: discountCentavos,
            totalFareCentavos: totalFareCentavos,
            distanceKm: distanceKm,
            matrixVersion: _matrix.Version,
            vehicleType: _matrix.VehicleType,
            passengerType: normalizedPassengerType,
            discountPercent: discountPercent,
            breakdown: breakdown);
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
    /// Round a centavo amount to the nearest 25 centavos.
    /// Requirement 2.4: Rounded to the nearest 25 centavos.
    /// </summary>
    public static int RoundToNearest25Centavos(double centavos)
    {
        return (int)(Math.Round(centavos / 25.0, MidpointRounding.AwayFromZero) * 25);
    }

    private int GetDiscountPercent(string normalizedCategory)
    {
        if (_matrix.DiscountPercentByCategory.TryGetValue(normalizedCategory, out int percent))
        {
            return percent;
        }

        return 0;
    }

    private static string NormalizePassengerType(string? passengerType)
    {
        if (string.IsNullOrWhiteSpace(passengerType))
        {
            return "regular";
        }

        return passengerType.Trim().ToLowerInvariant();
    }

    private static FareBreakdown BuildBreakdown(
        int baseFareCentavos,
        int distanceChargeCentavos,
        int subtotalCentavos,
        int discountPercent,
        string passengerType,
        double discountedAmount,
        int totalFareCentavos,
        double distanceKm)
    {
        string baseFareDesc = $"First {distanceKm:F1} km (minimum fare): ₱{baseFareCentavos / 100m:F2}";

        string distanceChargeDesc = distanceChargeCentavos > 0
            ? $"Distance charge: ₱{distanceChargeCentavos / 100m:F2}"
            : string.Empty;

        string subtotalDesc = $"Subtotal: ₱{subtotalCentavos / 100m:F2}";

        string discountDesc = discountPercent > 0
            ? $"{char.ToUpper(passengerType[0])}{passengerType[1..]} discount ({discountPercent}%): -₱{(subtotalCentavos - (int)Math.Round(discountedAmount, MidpointRounding.AwayFromZero)) / 100m:F2}"
            : string.Empty;

        string roundingDesc = $"Total (rounded to nearest ₱0.25): ₱{totalFareCentavos / 100m:F2}";

        return new FareBreakdown(
            baseFareDescription: baseFareDesc,
            distanceChargeDescription: distanceChargeDesc,
            subtotalDescription: subtotalDesc,
            discountDescription: discountDesc,
            roundingDescription: roundingDesc);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
