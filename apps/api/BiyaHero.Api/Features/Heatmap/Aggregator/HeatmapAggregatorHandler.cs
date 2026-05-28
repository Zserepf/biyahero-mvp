using System.Text.Json;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.Features.Heatmap.Aggregator;

/// <summary>
/// EventBridge-driven heatmap aggregator Lambda handler.
/// Triggered on a 5-second cadence by EventBridge (CDK wires the schedule rule).
///
/// Flow:
/// 1. Get all subscribed connections (those with a non-null bbox)
/// 2. For each unique bbox, query active demand pings from the relevant geohash5 partitions
/// 3. Aggregate pings by geohash7 + vehicle type (no PII — Req 4.6)
/// 4. Push heatmap.delta envelope to each subscribed connection via PostToConnection
/// 5. Handle stale connections gracefully (410 Gone → remove connection)
///
/// Requirements: 4.2, 4.3, 4.6
/// </summary>
public sealed class HeatmapAggregatorHandler
{
    private readonly IWsConnectionRepository _wsConnectionRepository;
    private readonly IDemandPingRepository _demandPingRepository;
    private readonly IWebSocketPushService _webSocketPushService;
    private readonly ILogger<HeatmapAggregatorHandler> _logger;

    public HeatmapAggregatorHandler(
        IWsConnectionRepository wsConnectionRepository,
        IDemandPingRepository demandPingRepository,
        IWebSocketPushService webSocketPushService,
        ILogger<HeatmapAggregatorHandler> logger)
    {
        _wsConnectionRepository = wsConnectionRepository;
        _demandPingRepository = demandPingRepository;
        _webSocketPushService = webSocketPushService;
        _logger = logger;
    }

    /// <summary>
    /// Main entry point invoked by EventBridge on a 5-second cadence.
    /// Aggregates active demand pings and pushes heatmap deltas to subscribed drivers.
    /// </summary>
    public async Task HandleAsync(CancellationToken cancellationToken = default)
    {
        // Step 1: Get all connections with an active heatmap bbox subscription.
        var subscribedConnections = await _wsConnectionRepository.GetSubscribedConnectionsAsync(cancellationToken);

        if (subscribedConnections.Count == 0)
        {
            _logger.LogDebug("No subscribed connections — skipping heatmap aggregation.");
            return;
        }

        // Step 2: Group connections by their subscribed bbox to avoid redundant queries.
        // Multiple drivers may subscribe to the same bbox — compute tiles once per unique bbox.
        var connectionsByBbox = subscribedConnections
            .Where(c => !string.IsNullOrEmpty(c.SubscribedBbox))
            .GroupBy(c => c.SubscribedBbox!)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Step 3: For each unique bbox, query and aggregate demand pings.
        var tilesByBbox = new Dictionary<string, List<HeatmapDeltaTile>>();

        foreach (var (bboxString, _) in connectionsByBbox)
        {
            var bbox = ParseBbox(bboxString);
            if (bbox is null)
            {
                _logger.LogWarning("Invalid bbox format: {Bbox}. Skipping.", bboxString);
                continue;
            }

            var tiles = await AggregateTilesForBboxAsync(bbox.Value, cancellationToken);
            tilesByBbox[bboxString] = tiles;
        }

        // Step 4: Push heatmap.delta envelope to each subscribed connection.
        var staleConnectionIds = new List<string>();

        foreach (var (bboxString, connections) in connectionsByBbox)
        {
            if (!tilesByBbox.TryGetValue(bboxString, out var tiles))
                continue;

            var envelope = BuildDeltaEnvelope(tiles);

            foreach (var connection in connections)
            {
                var delivered = await _webSocketPushService.PostToConnectionAsync(
                    connection.ConnectionId,
                    envelope,
                    cancellationToken);

                if (!delivered)
                {
                    // 410 Gone — connection is stale, mark for removal.
                    _logger.LogInformation(
                        "Connection {ConnectionId} is stale (410 Gone). Marking for removal.",
                        connection.ConnectionId);
                    staleConnectionIds.Add(connection.ConnectionId);
                }
            }
        }

        // Step 5: Remove stale connections gracefully.
        foreach (var connectionId in staleConnectionIds)
        {
            try
            {
                await _wsConnectionRepository.RemoveConnectionAsync(connectionId, cancellationToken);
                _logger.LogInformation("Removed stale connection {ConnectionId}.", connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove stale connection {ConnectionId}.", connectionId);
            }
        }

        _logger.LogInformation(
            "Heatmap aggregation complete. Pushed to {ConnectionCount} connections, removed {StaleCount} stale.",
            subscribedConnections.Count - staleConnectionIds.Count,
            staleConnectionIds.Count);
    }

    /// <summary>
    /// Queries active demand pings for a bbox and aggregates by geohash7 + vehicle type.
    /// No PII is included in the output (Req 4.6).
    /// </summary>
    private async Task<List<HeatmapDeltaTile>> AggregateTilesForBboxAsync(
        BboxCoordinates bbox,
        CancellationToken cancellationToken)
    {
        // Compute geohash5 cells covering the bbox for DynamoDB partition routing.
        var geohash5Cells = GeohashEncoder.GetGeohashesInBbox(
            bbox.MinLat, bbox.MinLng, bbox.MaxLat, bbox.MaxLng, precision: 5);

        // Query active demand pings from all relevant geohash5 partitions.
        var activePings = await _demandPingRepository.QueryByBboxAsync(geohash5Cells, cancellationToken);

        // Filter pings to only those within the exact bbox boundary
        // (geohash5 cells may extend beyond the bbox).
        var filteredPings = activePings.Where(p =>
            p.Latitude >= bbox.MinLat && p.Latitude <= bbox.MaxLat &&
            p.Longitude >= bbox.MinLng && p.Longitude <= bbox.MaxLng);

        // Aggregate by geohash7 + vehicle type — NO PII (Req 4.6).
        var tiles = filteredPings
            .GroupBy(p => (p.Geohash7, p.VehicleType))
            .Select(g => new HeatmapDeltaTile(
                Geohash7: g.Key.Geohash7,
                DemandCount: g.Count(),
                VehicleType: g.Key.VehicleType.ToString()))
            .ToList();

        return tiles;
    }

    /// <summary>
    /// Builds the heatmap.delta WebSocket envelope JSON string.
    /// Envelope format: { "action": "heatmap.delta", "requestId": "...", "data": { "tiles": [...] }, "emittedAt": "..." }
    /// </summary>
    private static string BuildDeltaEnvelope(List<HeatmapDeltaTile> tiles)
    {
        var envelope = new
        {
            action = "heatmap.delta",
            requestId = Guid.NewGuid().ToString(),
            data = new
            {
                tiles = tiles.Select(t => new
                {
                    geohash7 = t.Geohash7,
                    demandCount = t.DemandCount,
                    vehicleType = t.VehicleType
                }).ToArray()
            },
            emittedAt = DateTime.UtcNow.ToString("o")
        };

        return JsonSerializer.Serialize(envelope);
    }

    /// <summary>
    /// Parses a bbox string in the format "swLat,swLng,neLat,neLng" into coordinates.
    /// Returns null if the format is invalid.
    /// </summary>
    private static BboxCoordinates? ParseBbox(string bboxString)
    {
        var parts = bboxString.Split(',');
        if (parts.Length != 4)
            return null;

        if (!double.TryParse(parts[0], out var minLat) ||
            !double.TryParse(parts[1], out var minLng) ||
            !double.TryParse(parts[2], out var maxLat) ||
            !double.TryParse(parts[3], out var maxLng))
        {
            return null;
        }

        return new BboxCoordinates(minLat, minLng, maxLat, maxLng);
    }
}

/// <summary>
/// Internal struct representing parsed bbox coordinates.
/// </summary>
internal readonly record struct BboxCoordinates(
    double MinLat,
    double MinLng,
    double MaxLat,
    double MaxLng);

/// <summary>
/// DTO for a single tile in the heatmap.delta push.
/// Contains only geohash7, demand count, and vehicle type — no PII (Req 4.6).
/// </summary>
public record HeatmapDeltaTile(
    string Geohash7,
    int DemandCount,
    string VehicleType);
