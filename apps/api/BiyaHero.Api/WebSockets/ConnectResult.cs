using BiyaHero.Api.Domain;

namespace BiyaHero.Api.WebSockets;

/// <summary>
/// Result of the WebSocket $connect handler.
/// Either the connection is accepted (IsAccepted = true) or rejected with a 4001 close code.
/// </summary>
public sealed record ConnectResult
{
    /// <summary>Whether the connection was accepted.</summary>
    public bool IsAccepted { get; init; }

    /// <summary>The authenticated user's ID (set on success).</summary>
    public Guid? UserId { get; init; }

    /// <summary>The authenticated user's role (set on success).</summary>
    public UserRole? Role { get; init; }

    /// <summary>Close code to send on rejection (4001 per Req 4.7, 5.4).</summary>
    public int CloseCode { get; init; }

    /// <summary>Human-readable reason for rejection.</summary>
    public string? CloseReason { get; init; }

    /// <summary>
    /// Creates a successful connection result.
    /// </summary>
    public static ConnectResult Ok(Guid userId, UserRole role) => new()
    {
        IsAccepted = true,
        UserId = userId,
        Role = role
    };

    /// <summary>
    /// Creates an authentication failure result with close code 4001.
    /// The caller must force-close the connection even if the close-frame fails to flush (Req 4.7).
    /// </summary>
    public static ConnectResult AuthFailure(string reason) => new()
    {
        IsAccepted = false,
        CloseCode = 4001,
        CloseReason = reason
    };
}
