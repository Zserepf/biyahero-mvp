using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.WebSockets;

/// <summary>
/// Native ASP.NET WebSocket endpoint for local development.
/// Replaces the AWS API Gateway WebSocket infrastructure with a direct
/// ws://localhost:5000/ws connection that handles the same message protocol.
///
/// Supports: demand-ping, cancel-demand, subscribe-heatmap
/// Auth: JWT token passed as ?token= query parameter
/// </summary>
public static class LocalWebSocketEndpoint
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };

    private static readonly JsonSerializerOptions _jsonReadOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };

    // In-memory demand pings: commuterId → DemandPingData
    private static readonly ConcurrentDictionary<Guid, DemandPingData> ActivePings = new();

    // In-memory WebSocket sessions: connectionId → WsSession
    private static readonly ConcurrentDictionary<string, WsSession> Sessions = new();

    public static void MapLocalWebSocketEndpoint(this WebApplication app)
    {
        app.UseWebSockets();

        app.Map("/ws", async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            // Extract JWT from query string
            var token = context.Request.Query["token"].ToString();
            var jwtService = context.RequestServices.GetRequiredService<IJwtService>();

            Guid? userId = null;
            UserRole role = UserRole.Commuter;

            if (!string.IsNullOrWhiteSpace(token))
            {
                var validation = await jwtService.ValidateTokenDetailedAsync(token);
                if (validation.IsValid && validation.UserId.HasValue)
                {
                    userId = validation.UserId;
                    if (Enum.TryParse<UserRole>(validation.Role, ignoreCase: true, out var parsedRole))
                        role = parsedRole;
                }
            }

            var ws = await context.WebSockets.AcceptWebSocketAsync();
            var connectionId = Guid.NewGuid().ToString("N");
            var session = new WsSession(connectionId, userId, role, ws);
            Sessions[connectionId] = session;

            var logger = context.RequestServices.GetRequiredService<ILogger<LocalWebSocketEndpointLogger>>();
            logger.LogInformation("WS connected: connectionId={ConnectionId}, userId={UserId}", connectionId, userId);

            // Send connected acknowledgement
            await SendAsync(ws, new
            {
                action = "connected",
                requestId = "server",
                data = new { connectionId, authenticated = userId.HasValue }
            });

            try
            {
                await HandleMessagesAsync(ws, session, logger);
            }
            finally
            {
                Sessions.TryRemove(connectionId, out _);

                // Clean up any active ping for this user
                if (userId.HasValue)
                    ActivePings.TryRemove(userId.Value, out _);

                logger.LogInformation("WS disconnected: connectionId={ConnectionId}", connectionId);
            }
        });
    }

    private static async Task HandleMessagesAsync(WebSocket ws, WsSession session, ILogger logger)
    {
        var buffer = new byte[4096];

        try
        {
        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            using var ms = new System.IO.MemoryStream();

            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                    return;
                }
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(ms.ToArray());

            try
            {
                var envelope = JsonSerializer.Deserialize<WsIncomingEnvelope>(json, _jsonReadOptions);

                if (envelope?.Action == null) continue;

                await DispatchAsync(ws, session, envelope, logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WS message parse error for connectionId={ConnectionId}", session.ConnectionId);
            }
        }
        }
        catch (System.Net.WebSockets.WebSocketException)
        {
            // Client disconnected abruptly (network drop, browser tab closed, etc.)
            // This is normal — log at debug level and let the finally block clean up.
            logger.LogDebug("WS abrupt disconnect: connectionId={ConnectionId}", session.ConnectionId);
        }
    }

    private static async Task DispatchAsync(WebSocket ws, WsSession session, WsIncomingEnvelope envelope, ILogger logger)
    {
        switch (envelope.Action)
        {
            case "demand-ping":
                await HandleDemandPingAsync(ws, session, envelope, logger);
                break;

            case "cancel-demand":
                await HandleCancelDemandAsync(ws, session, envelope, logger);
                break;

            case "subscribe-heatmap":
                await HandleSubscribeHeatmapAsync(ws, session, envelope, logger);
                break;

            default:
                await SendAsync(ws, new
                {
                    action = "error",
                    requestId = envelope.RequestId ?? "unknown",
                    data = new { message = $"Unknown action: {envelope.Action}" }
                });
                break;
        }
    }

    private static async Task HandleDemandPingAsync(WebSocket ws, WsSession session, WsIncomingEnvelope envelope, ILogger logger)
    {
        if (!session.UserId.HasValue)
        {
            await ws.CloseAsync((WebSocketCloseStatus)4001, "Authentication required", CancellationToken.None);
            return;
        }

        double lat = 0, lng = 0;
        string vehicleType = "jeepney";

        if (envelope.Data.HasValue && envelope.Data.Value.ValueKind == JsonValueKind.Object)
        {
            if (envelope.Data.Value.TryGetProperty("lat", out var latEl)) lat = latEl.GetDouble();
            if (envelope.Data.Value.TryGetProperty("lng", out var lngEl)) lng = lngEl.GetDouble();
            if (envelope.Data.Value.TryGetProperty("vehicleType", out var vtEl)) vehicleType = vtEl.GetString() ?? "jeepney";
        }

        var pingId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddMinutes(5);

        ActivePings[session.UserId.Value] = new DemandPingData(pingId, lat, lng, vehicleType, expiresAt);

        logger.LogInformation("demand-ping: userId={UserId}, lat={Lat}, lng={Lng}, vehicle={Vehicle}", session.UserId, lat, lng, vehicleType);

        await SendAsync(ws, new
        {
            action = "demand-ping",
            requestId = envelope.RequestId ?? Guid.NewGuid().ToString(),
            data = new { pingId, lat, lng, vehicleType, expiresAt = expiresAt.ToString("O") }
        });

        // Broadcast heatmap delta to all subscribed driver sessions
        await BroadcastHeatmapDeltaAsync(lat, lng, vehicleType, 1);
    }

    private static async Task HandleCancelDemandAsync(WebSocket ws, WsSession session, WsIncomingEnvelope envelope, ILogger logger)
    {
        if (!session.UserId.HasValue)
        {
            await ws.CloseAsync((WebSocketCloseStatus)4001, "Authentication required", CancellationToken.None);
            return;
        }

        var cancelled = ActivePings.TryRemove(session.UserId.Value, out var removedPing);

        if (cancelled && removedPing != null)
        {
            // Broadcast removal to drivers
            await BroadcastHeatmapDeltaAsync(removedPing.Lat, removedPing.Lng, removedPing.VehicleType, 0);
        }

        await SendAsync(ws, new
        {
            action = "cancel-demand",
            requestId = envelope.RequestId ?? Guid.NewGuid().ToString(),
            data = new { cancelled }
        });
    }

    private static async Task HandleSubscribeHeatmapAsync(WebSocket ws, WsSession session, WsIncomingEnvelope envelope, ILogger logger)
    {
        // Store bbox subscription on session
        if (envelope.Data.HasValue && envelope.Data.Value.ValueKind == JsonValueKind.Object)
        {
            var data = envelope.Data.Value;
            if (data.TryGetProperty("bbox", out var bboxEl))
            {
                double swLat = 0, swLng = 0, neLat = 0, neLng = 0;
                if (bboxEl.TryGetProperty("swLat", out var swLatEl)) swLat = swLatEl.GetDouble();
                if (bboxEl.TryGetProperty("swLng", out var swLngEl)) swLng = swLngEl.GetDouble();
                if (bboxEl.TryGetProperty("neLat", out var neLatEl)) neLat = neLatEl.GetDouble();
                if (bboxEl.TryGetProperty("neLng", out var neLngEl)) neLng = neLngEl.GetDouble();
                session.SubscribedBbox = (swLat, swLng, neLat, neLng);
            }
        }

        // Send current active pings as initial heatmap delta
        var tiles = BuildHeatmapTiles();

        await SendAsync(ws, new
        {
            action = "heatmap.delta",
            requestId = envelope.RequestId ?? Guid.NewGuid().ToString(),
            data = new { tiles }
        });
    }

    private static async Task BroadcastHeatmapDeltaAsync(double lat, double lng, string vehicleType, int demandCount)
    {
        var tiles = BuildHeatmapTiles();
        var message = new
        {
            action = "heatmap.delta",
            requestId = "server",
            data = new { tiles }
        };

        foreach (var (_, s) in Sessions)
        {
            if (s.WebSocket.State == WebSocketState.Open && s.SubscribedBbox.HasValue)
            {
                try { await SendAsync(s.WebSocket, message); }
                catch { /* ignore closed connections */ }
            }
        }
    }

    private static List<object> BuildHeatmapTiles()
    {
        // Group active pings by approximate geohash7 (simplified: round to 3 decimal places)
        var groups = ActivePings.Values
            .GroupBy(p => $"{Math.Round(p.Lat, 3)},{Math.Round(p.Lng, 3)}")
            .Select(g => new
            {
                geohash7 = g.Key.Replace(",", "_").Replace(".", "d"),
                lat = g.Average(p => p.Lat),
                lng = g.Average(p => p.Lng),
                demandCount = g.Count(),
                vehicleTypes = g.Select(p => p.VehicleType).Distinct().ToList()
            })
            .Cast<object>()
            .ToList();

        return groups;
    }

    private static async Task SendAsync(WebSocket ws, object payload)
    {
        if (ws.State != WebSocketState.Open) return;

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}

/// <summary>In-memory WebSocket session.</summary>
public sealed class WsSession(string connectionId, Guid? userId, UserRole role, WebSocket webSocket)
{
    public string ConnectionId { get; } = connectionId;
    public Guid? UserId { get; } = userId;
    public UserRole Role { get; } = role;
    public WebSocket WebSocket { get; } = webSocket;
    public (double SwLat, double SwLng, double NeLat, double NeLng)? SubscribedBbox { get; set; }
}

/// <summary>In-memory demand ping data.</summary>
public sealed record DemandPingData(Guid PingId, double Lat, double Lng, string VehicleType, DateTime ExpiresAt);

/// <summary>Placeholder logger type for the static endpoint.</summary>
internal sealed class LocalWebSocketEndpointLogger { }
