namespace BiyaHero.Api.Features.Fare;

/// <summary>
/// Repository interface for fare matrix data access.
/// Provides queries specific to the fare_matrices table beyond generic CRUD.
/// </summary>
public interface IFareMatrixRepository
{
    /// <summary>
    /// Loads all currently active fare matrices (latest effective_at that is not in the future),
    /// grouped by vehicle type. Returns one matrix per vehicle type.
    /// </summary>
    Task<IReadOnlyList<FareMatrix>> GetActiveMatricesAsync();

    /// <summary>
    /// Loads the active fare matrix for a specific vehicle type.
    /// Returns null if no matrix exists for the given vehicle type.
    /// </summary>
    Task<FareMatrix?> GetActiveMatrixByVehicleTypeAsync(string vehicleType);
}
