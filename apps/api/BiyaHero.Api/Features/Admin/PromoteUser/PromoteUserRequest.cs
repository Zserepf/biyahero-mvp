namespace BiyaHero.Api.Features.Admin.PromoteUser;

/// <summary>
/// Request body for the promote user operation.
/// Requires the acting Super Admin's password as 2FA confirmation (Req 5.11)
/// and the target role to assign.
/// </summary>
public sealed record PromoteUserRequest(
    string Password,
    string NewRole);
