using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Fare;
using FsCheck;
using FsCheck.Xunit;
using Xunit;
using DomainFareCalculator = BiyaHero.Api.Domain.FareCalculator;

namespace BiyaHero.Api.Tests.Properties;

/// <summary>
/// Property-based tests for fare calculation determinism.
/// Feature: biyahero-mvp, Property 3: Determinism of fare calculation
/// 
/// **Validates: Requirements 2.10**
/// 
/// FOR ALL valid input pairs (origin, destination, vehicle, discount),
/// the Fare_Calculator SHALL return identical results when invoked twice
/// with identical inputs (deterministic property).
/// </summary>
[Trait("Feature", "biyahero-mvp")]
[Trait("Property", "Property 3: Determinism of fare calculation")]
public class FareDeterminismPropertyTests
{
    // Philippines bounding box
    private const double MinLat = 4.5;
    private const double MaxLat = 21.5;
    private const double MinLng = 116.0;
    private const double MaxLng = 127.0;

    private static readonly string[] VehicleTypes = { "jeepney", "bus", "uv_express", "tricycle" };
    private static readonly string[] PassengerCategories = { "regular", "student", "senior", "pwd" };

    /// <summary>
    /// Generator for a latitude within the Philippines bounding box.
    /// </summary>
    private static Gen<double> PhilippinesLat =>
        Gen.Choose(4500, 21500).Select(x => x / 1000.0);

    /// <summary>
    /// Generator for a longitude within the Philippines bounding box.
    /// </summary>
    private static Gen<double> PhilippinesLng =>
        Gen.Choose(116000, 127000).Select(x => x / 1000.0);

    /// <summary>
    /// Generator for a vehicle type string.
    /// </summary>
    private static Gen<string> VehicleTypeGen =>
        Gen.Elements(VehicleTypes);

    /// <summary>
    /// Generator for a passenger category string.
    /// </summary>
    private static Gen<string> PassengerCategoryGen =>
        Gen.Elements(PassengerCategories);

    /// <summary>
    /// Arbitrary that produces valid fare calculation inputs within the Philippines bbox.
    /// </summary>
    private static Arbitrary<(double OriginLat, double OriginLng, double DestLat, double DestLng, string VehicleType, string PassengerCategory)> FareInputArbitrary()
    {
        var gen = from originLat in PhilippinesLat
                  from originLng in PhilippinesLng
                  from destLat in PhilippinesLat
                  from destLng in PhilippinesLng
                  from vehicleType in VehicleTypeGen
                  from passengerCategory in PassengerCategoryGen
                  select (originLat, originLng, destLat, destLng, vehicleType, passengerCategory);

        return Arb.From(gen);
    }

    /// <summary>
    /// Creates a FareMatrix for the given vehicle type with realistic LTFRB values.
    /// </summary>
    private static FareMatrix CreateFareMatrix(string vehicleType)
    {
        var discounts = new Dictionary<string, int>
        {
            ["regular"] = 0,
            ["student"] = 20,
            ["senior"] = 20,
            ["pwd"] = 20
        };

        // Use realistic LTFRB fare values based on vehicle type
        return vehicleType switch
        {
            "jeepney" => new FareMatrix("v1", "jeepney", 1300, 4.0, 180, discounts),
            "bus" => new FareMatrix("v1", "bus", 1500, 5.0, 270, discounts),
            "uv_express" => new FareMatrix("v1", "uv_express", 3000, 5.0, 230, discounts),
            "tricycle" => new FareMatrix("v1", "tricycle", 4000, 2.0, 500, discounts),
            _ => new FareMatrix("v1", vehicleType, 1300, 4.0, 180, discounts)
        };
    }

    /// <summary>
    /// Property 3: Determinism — calling FareCalculator.Calculate() twice with
    /// identical inputs produces identical TotalFareCentavos results.
    /// 
    /// **Validates: Requirements 2.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FareCalculation_IsDeterministic()
    {
        return Prop.ForAll(
            FareInputArbitrary(),
            input =>
            {
                var matrix = CreateFareMatrix(input.VehicleType);
                var calculator = new DomainFareCalculator(matrix);

                var result1 = calculator.Calculate(
                    input.OriginLat,
                    input.OriginLng,
                    input.DestLat,
                    input.DestLng,
                    input.PassengerCategory);

                var result2 = calculator.Calculate(
                    input.OriginLat,
                    input.OriginLng,
                    input.DestLat,
                    input.DestLng,
                    input.PassengerCategory);

                return (result1.TotalFareCentavos == result2.TotalFareCentavos)
                    .Label($"Determinism: first={result1.TotalFareCentavos}, second={result2.TotalFareCentavos}")
                    .And(() => (result1.BaseFareCentavos == result2.BaseFareCentavos)
                        .Label("BaseFare mismatch"))
                    .And(() => (result1.DistanceChargeCentavos == result2.DistanceChargeCentavos)
                        .Label("DistanceCharge mismatch"))
                    .And(() => (result1.DiscountCentavos == result2.DiscountCentavos)
                        .Label("Discount mismatch"))
                    .And(() => (result1.DistanceKm == result2.DistanceKm)
                        .Label("DistanceKm mismatch"));
            });
    }

    /// <summary>
    /// Property 3 (supplementary): The total fare is always a multiple of 25 centavos.
    /// This validates the rounding rule from Requirement 2.4.
    /// 
    /// **Validates: Requirements 2.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FareCalculation_ResultIsMultipleOf25Centavos()
    {
        return Prop.ForAll(
            FareInputArbitrary(),
            input =>
            {
                var matrix = CreateFareMatrix(input.VehicleType);
                var calculator = new DomainFareCalculator(matrix);

                var result = calculator.Calculate(
                    input.OriginLat,
                    input.OriginLng,
                    input.DestLat,
                    input.DestLng,
                    input.PassengerCategory);

                return (result.TotalFareCentavos % 25 == 0)
                    .Label($"TotalFareCentavos={result.TotalFareCentavos} is not a multiple of 25");
            });
    }
}
