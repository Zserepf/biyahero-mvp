namespace BiyaHero.Api.Features.Admin.PromoteUser;

/// <summary>
/// Response DTO for the promote user operation.
/// </summary>
public sealed record PromoteUserResponse(
    Guid UserId,
    string NewRole,
    string Message);
