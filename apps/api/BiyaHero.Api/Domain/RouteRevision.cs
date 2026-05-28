namespace BiyaHero.Api.Domain;

/// <summary>
/// A pending edit to an existing Route, submitted by a commuter.
/// Stored as a separate entity linked to the original Route without overwriting the verified version.
/// Only applied when a Moderator approves it (Req 1.3, 1.4).
/// </summary>
public class RouteRevision : BaseDomain
{
    public Guid RouteId { get; set; }
    public Guid SubmittedBy { get; set; }
    public RevisionStatus Status { get; set; }
    public Guid? ApproverId { get; set; }
    public List<Waypoint> Waypoints { get; set; } = new();

    public RouteRevision() : base() { }

    public RouteRevision(
        Guid id,
        DateTime createdAt,
        DateTime updatedAt,
        Guid routeId,
        Guid submittedBy,
        RevisionStatus status,
        List<Waypoint> waypoints,
        Guid? approverId = null)
        : base(id, createdAt, updatedAt)
    {
        RouteId = routeId;
        SubmittedBy = submittedBy;
        Status = status;
        Waypoints = waypoints;
        ApproverId = approverId;
    }

    /// <summary>
    /// Serialize this RouteRevision to a JSON-compatible dictionary.
    /// </summary>
    public override Dictionary<string, object?> Serialize()
    {
        var dict = base.Serialize();
        dict["routeId"] = RouteId.ToString();
        dict["submittedBy"] = SubmittedBy.ToString();
        dict["status"] = Status.ToString();
        dict["approverId"] = ApproverId?.ToString();
        dict["waypoints"] = Waypoints
            .OrderBy(w => w.SequenceOrder)
            .Select(w => w.Serialize())
            .ToList();
        return dict;
    }

    /// <summary>
    /// Parse a serialized dictionary back into a RouteRevision instance.
    /// </summary>
    public static RouteRevision Parse(Dictionary<string, object?> data)
    {
        var id = Guid.Parse(data["id"]?.ToString() ?? throw new ArgumentException("Missing id"));
        var createdAt = DateTime.Parse(data["createdAt"]?.ToString() ?? throw new ArgumentException("Missing createdAt"));
        var updatedAt = DateTime.Parse(data["updatedAt"]?.ToString() ?? throw new ArgumentException("Missing updatedAt"));
        var routeId = Guid.Parse(data["routeId"]?.ToString() ?? throw new ArgumentException("Missing routeId"));
        var submittedBy = Guid.Parse(data["submittedBy"]?.ToString() ?? throw new ArgumentException("Missing submittedBy"));
        var status = Enum.Parse<RevisionStatus>(data["status"]?.ToString() ?? throw new ArgumentException("Missing status"));

        Guid? approverId = null;
        if (data.TryGetValue("approverId", out var approverVal) && approverVal != null)
        {
            approverId = Guid.Parse(approverVal.ToString()!);
        }

        var waypoints = new List<Waypoint>();
        if (data.TryGetValue("waypoints", out var waypointsVal) && waypointsVal is IEnumerable<object> waypointsList)
        {
            foreach (var wp in waypointsList)
            {
                if (wp is Dictionary<string, object?> wpDict)
                {
                    waypoints.Add(Waypoint.Parse(wpDict));
                }
            }
        }

        return new RouteRevision(id, createdAt, updatedAt, routeId, submittedBy, status, waypoints, approverId);
    }
}
