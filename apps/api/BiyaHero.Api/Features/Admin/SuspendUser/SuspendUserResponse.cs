namespace BiyaHero.Api.Features.Admin.SuspendUser;

/// <summary>
/// Response DTO for the suspend user operation.
/// </summary>
public sealed record SuspendUserResponse(
    Guid UserId,
    string Status,
    string Message);
