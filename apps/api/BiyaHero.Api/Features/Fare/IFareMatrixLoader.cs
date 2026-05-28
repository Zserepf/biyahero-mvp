namespace BiyaHero.Api.Features.Fare;

/// <summary>
/// Service interface for loading the active LTFRB fare matrix.
/// Implementations cache the matrix in-process and support refresh without redeployment.
/// </summary>
public interface IFareMatrixLoader
{
    /// <summary>
    /// Gets the active fare matrix for the specified vehicle type.
    /// Returns the cached version if available and not expired; otherwise reloads from the database.
    /// </summary>
    Task<FareMatrix?> GetActiveMatrixAsync(string vehicleType);

    /// <summary>
    /// Gets all active fare matrices (one per vehicle type).
    /// Returns cached versions if available and not expired; otherwise reloads from the database.
    /// </summary>
    Task<IReadOnlyList<FareMatrix>> GetAllActiveMatricesAsync();

    /// <summary>
    /// Forces a cache refresh. Call this from a refresh endpoint or admin action
    /// to pick up new fare matrix versions without redeploying.
    /// </summary>
    Task RefreshAsync();
}
