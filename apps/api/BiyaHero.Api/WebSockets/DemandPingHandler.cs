using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;

namespace BiyaHero.Api.WebSockets;

/// <summary>
/// Handles the WebSocket "demand-ping" route message from Commuters.
///
/// Flow:
/// 1. Look up the connection to verify the user is authenticated — 4001 close if not
/// 2. Validate coordinates within Philippines bbox (lat 4.5°–21.5° N, lng 116°–127° E)
/// 3. Validate vehicle type is a supported enum value
/// 4. Encode geohash5 and geohash7 from the coordinates
/// 5. Persist the DemandPing in DynamoDB with 5-minute TTL
/// 6. Return success envelope to the caller
///
/// Requirements: 4.1, 4.7, 4.8, 4.9
/// </summary>
public sealed class DemandPingHandler
{
    private const int AuthFailureCloseCode = 4001;
    private const int TtlMinutes = 5;

    // Philippines bounding box (Req 4.9)
    private const double MinLatitude = 4.5;
    private const double MaxLatitude = 21.5;
    private const double MinLongitude = 116.0;
    private const double MaxLongitude = 127.0;

    private readonly IWsConnectionRepository _wsConnectionRepository;
    private readonly IDemandPingRepository _demandPingRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DemandPingHandler> _logger;

    public DemandPingHandler(
        IWsConnectionRepository wsConnectionRepository,
        IDemandPingRepository demandPingRepository,
        TimeProvider timeProvider,
        ILogger<DemandPingHandler> logger)
    {
        _wsConnectionRepository = wsConnectionRepository;
        _demandPingRepository = demandPingRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Processes a demand-ping WebSocket message.
    /// </summary>
    /// <param name="connectionId">The API Gateway WebSocket connection ID.</param>
    /// <param name="request">The parsed demand-ping request data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="DemandPingResult"/> indicating success, validation error, or auth failure.</returns>
    public async Task<DemandPingResult> HandleAsync(
        string connectionId,
        DemandPingRequest request,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Look up connection to verify authentication (Req 4.7)
        var connection = await _wsConnectionRepository.GetConnectionByIdAsync(connectionId, cancellationToken);

        if (connection is null)
        {
            _logger.LogWarning(
                "demand-ping rejected for connectionId={ConnectionId}: no connection record found (unauthenticated)",
                connectionId);
            return DemandPingResult.AuthFailure("Authentication required to submit demand pings.");
        }

        var commuterId = connection.UserId;

        // Step 2: Validate coordinates within Philippines bbox (Req 4.8, 4.9)
        if (!IsWithinPhilippinesBbox(request.Latitude, request.Longitude))
        {
            _logger.LogWarning(
                "demand-ping rejected for connectionId={ConnectionId}: coordinates ({Lat}, {Lng}) outside Philippines bbox",
                connectionId, request.Latitude, request.Longitude);
            return DemandPingResult.ValidationError(
                "Invalid coordinates. Latitude must be between 4.5° and 21.5° N, longitude between 116° and 127° E.");
        }

        // Step 3: Validate vehicle type (Req 4.8)
        if (!Enum.IsDefined(typeof(VehicleType), request.VehicleType))
        {
            _logger.LogWarning(
                "demand-ping rejected for connectionId={ConnectionId}: unsupported vehicle type '{VehicleType}'",
                connectionId, request.VehicleType);
            return DemandPingResult.ValidationError(
                $"Unsupported vehicle type. Valid types: {string.Join(", ", Enum.GetNames<VehicleType>())}.");
        }

        // Step 4: Encode geohash5 and geohash7
        var geohash5 = GeohashEncoder.EncodeForPartition(request.Latitude, request.Longitude);
        var geohash7 = GeohashEncoder.EncodeForTile(request.Latitude, request.Longitude);

        // Step 5: Persist DemandPing with 5-minute TTL (Req 4.1)
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var pingId = Guid.NewGuid();

        var demandPing = new DemandPing(
            id: pingId,
            createdAt: now,
            updatedAt: now,
            commuterId: commuterId,
            latitude: request.Latitude,
            longitude: request.Longitude,
            geohash5: geohash5,
            geohash7: geohash7,
            vehicleType: request.VehicleType,
            expiresAt: now.AddMinutes(TtlMinutes));

        await _demandPingRepository.PutPingAsync(demandPing, cancellationToken);

        _logger.LogInformation(
            "demand-ping persisted: pingId={PingId}, commuterId={CommuterId}, geohash5={Geohash5}, vehicleType={VehicleType}",
            pingId, commuterId, geohash5, request.VehicleType);

        // Step 6: Return success
        return DemandPingResult.Success(pingId, geohash7, demandPing.ExpiresAt);
    }

    /// <summary>
    /// Validates that coordinates fall within the Philippines bounding box.
    /// Latitude: 4.5° to 21.5° North
    /// Longitude: 116° to 127° East
    /// </summary>
    private static bool IsWithinPhilippinesBbox(double latitude, double longitude)
    {
        return latitude >= MinLatitude
            && latitude <= MaxLatitude
            && longitude >= MinLongitude
            && longitude <= MaxLongitude;
    }
}

/// <summary>
/// Request data extracted from the demand-ping WebSocket message payload.
/// </summary>
public sealed record DemandPingRequest
{
    /// <summary>Latitude of the commuter's location.</summary>
    public required double Latitude { get; init; }

    /// <summary>Longitude of the commuter's location.</summary>
    public required double Longitude { get; init; }

    /// <summary>The type of vehicle the commuter is waiting for.</summary>
    public required VehicleType VehicleType { get; init; }
}

/// <summary>
/// Result of the demand-ping handler.
/// Can be success, validation error (no persistence), or auth failure (4001 close).
/// </summary>
public sealed record DemandPingResult
{
    public bool IsSuccess { get; init; }
    public bool IsAuthFailure { get; init; }
    public int? CloseCode { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? PingId { get; init; }
    public string? Geohash7 { get; init; }
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Creates a successful result with the persisted ping details.
    /// </summary>
    public static DemandPingResult Success(Guid pingId, string geohash7, DateTime expiresAt) => new()
    {
        IsSuccess = true,
        PingId = pingId,
        Geohash7 = geohash7,
        ExpiresAt = expiresAt
    };

    /// <summary>
    /// Creates a validation error result. The ping is NOT persisted (Req 4.8).
    /// </summary>
    public static DemandPingResult ValidationError(string message) => new()
    {
        IsSuccess = false,
        IsAuthFailure = false,
        ErrorMessage = message
    };

    /// <summary>
    /// Creates an authentication failure result with close code 4001 (Req 4.7).
    /// The caller must force-close the connection.
    /// </summary>
    public static DemandPingResult AuthFailure(string message) => new()
    {
        IsSuccess = false,
        IsAuthFailure = true,
        CloseCode = 4001,
        ErrorMessage = message
    };
}
