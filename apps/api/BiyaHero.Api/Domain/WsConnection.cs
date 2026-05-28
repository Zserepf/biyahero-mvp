namespace BiyaHero.Api.Domain;

/// <summary>
/// Represents an active WebSocket connection tracked in DynamoDB.
/// PK is USER#{userId}, SK is CONN#{connectionId}.
/// Used to look up whether a driver is connected (for payment fan-out)
/// and to store heatmap bbox subscriptions.
/// Carries a 24-hour TTL as a safety net for orphaned connections.
/// </summary>
public class WsConnection : BaseDomain
{
    /// <summary>The authenticated user who owns this connection.</summary>
    public Guid UserId { get; set; }

    /// <summary>The role of the connected user (Commuter, Driver, etc.).</summary>
    public UserRole Role { get; set; }

    /// <summary>The API Gateway WebSocket connection ID.</summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>When this connection was established.</summary>
    public DateTime ConnectedAt { get; set; }

    /// <summary>
    /// Optional bounding box the user has subscribed to for heatmap updates.
    /// Null if the user has not subscribed to heatmap deltas.
    /// Stored as "swLat,swLng,neLat,neLng" string for DynamoDB compatibility.
    /// </summary>
    public string? SubscribedBbox { get; set; }

    /// <summary>Safety-net TTL (24 hours from connection) for orphaned connections.</summary>
    public DateTime ExpiresAt { get; set; }

    public WsConnection() : base() { }

    public WsConnection(
        Guid id,
        DateTime createdAt,
        DateTime updatedAt,
        Guid userId,
        UserRole role,
        string connectionId,
        DateTime connectedAt,
        string? subscribedBbox,
        DateTime expiresAt)
        : base(id, createdAt, updatedAt)
    {
        UserId = userId;
        Role = role;
        ConnectionId = connectionId;
        ConnectedAt = connectedAt;
        SubscribedBbox = subscribedBbox;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Serialize this WsConnection to a JSON-compatible dictionary.
    /// </summary>
    public override Dictionary<string, object?> Serialize()
    {
        var dict = base.Serialize();
        dict["userId"] = UserId.ToString();
        dict["role"] = Role.ToString();
        dict["connectionId"] = ConnectionId;
        dict["connectedAt"] = ConnectedAt.ToString("o");
        dict["subscribedBbox"] = SubscribedBbox;
        dict["expiresAt"] = ExpiresAt.ToString("o");
        return dict;
    }

    /// <summary>
    /// Parse a serialized dictionary back into a WsConnection instance.
    /// Inverse of Serialize() for round-trip verification.
    /// </summary>
    public static WsConnection Parse(Dictionary<string, object?> data)
    {
        var id = Guid.Parse(data["id"]?.ToString() ?? throw new ArgumentException("Missing id"));
        var createdAt = DateTime.Parse(data["createdAt"]?.ToString() ?? throw new ArgumentException("Missing createdAt"));
        var updatedAt = DateTime.Parse(data["updatedAt"]?.ToString() ?? throw new ArgumentException("Missing updatedAt"));
        var userId = Guid.Parse(data["userId"]?.ToString() ?? throw new ArgumentException("Missing userId"));
        var role = Enum.Parse<UserRole>(data["role"]?.ToString() ?? throw new ArgumentException("Missing role"));
        var connectionId = data["connectionId"]?.ToString() ?? throw new ArgumentException("Missing connectionId");
        var connectedAt = DateTime.Parse(data["connectedAt"]?.ToString() ?? throw new ArgumentException("Missing connectedAt"));
        var subscribedBbox = data["subscribedBbox"]?.ToString();
        var expiresAt = DateTime.Parse(data["expiresAt"]?.ToString() ?? throw new ArgumentException("Missing expiresAt"));

        return new WsConnection(id, createdAt, updatedAt, userId, role, connectionId, connectedAt, subscribedBbox, expiresAt);
    }
}
