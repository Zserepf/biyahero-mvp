namespace BiyaHero.Api.Domain;

/// <summary>
/// A commuter's accuracy vote on a verified route.
/// One vote per user per route (enforced at the database level via UNIQUE constraint).
/// Records whether the commuter believes the route is still accurate or no longer accurate (Req 1.5).
/// </summary>
public class RouteVote : BaseDomain
{
    public Guid RouteId { get; set; }
    public Guid VoterId { get; set; }
    public VoteKind Kind { get; set; }
    public DateTime Timestamp { get; set; }

    public RouteVote() : base()
    {
        Timestamp = DateTime.UtcNow;
    }

    public RouteVote(
        Guid id,
        DateTime createdAt,
        DateTime updatedAt,
        Guid routeId,
        Guid voterId,
        VoteKind kind,
        DateTime timestamp)
        : base(id, createdAt, updatedAt)
    {
        RouteId = routeId;
        VoterId = voterId;
        Kind = kind;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Serialize this RouteVote to a JSON-compatible dictionary.
    /// </summary>
    public override Dictionary<string, object?> Serialize()
    {
        var dict = base.Serialize();
        dict["routeId"] = RouteId.ToString();
        dict["voterId"] = VoterId.ToString();
        dict["kind"] = Kind.ToString();
        dict["timestamp"] = Timestamp.ToString("o");
        return dict;
    }

    /// <summary>
    /// Parse a serialized dictionary back into a RouteVote instance.
    /// </summary>
    public static RouteVote Parse(Dictionary<string, object?> data)
    {
        var id = Guid.Parse(data["id"]?.ToString() ?? throw new ArgumentException("Missing id"));
        var createdAt = DateTime.Parse(data["createdAt"]?.ToString() ?? throw new ArgumentException("Missing createdAt"));
        var updatedAt = DateTime.Parse(data["updatedAt"]?.ToString() ?? throw new ArgumentException("Missing updatedAt"));
        var routeId = Guid.Parse(data["routeId"]?.ToString() ?? throw new ArgumentException("Missing routeId"));
        var voterId = Guid.Parse(data["voterId"]?.ToString() ?? throw new ArgumentException("Missing voterId"));
        var kind = Enum.Parse<VoteKind>(data["kind"]?.ToString() ?? throw new ArgumentException("Missing kind"));
        var timestamp = DateTime.Parse(data["timestamp"]?.ToString() ?? throw new ArgumentException("Missing timestamp"));

        return new RouteVote(id, createdAt, updatedAt, routeId, voterId, kind, timestamp);
    }
}
