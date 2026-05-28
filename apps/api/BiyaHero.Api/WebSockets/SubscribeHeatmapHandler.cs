using System.Text.Json;
using BiyaHero.Api.Repositories;

namespace BiyaHero.Api.WebSockets;

/// <summary>
/// Handles the "subscribe-heatmap" WebSocket route.
///
/// Flow:
/// 1. Parse the bbox from the incoming message data (minLat, minLng, maxLat, maxLng)
/// 2. Validate coordinates are within valid lat/lng ranges
/// 3. Store the bbox subscription on the connection record via UpdateSubscriptionAsync
/// 4. Return a success envelope to the caller
///
/// Anonymous subscribe IS allowed — drivers can view heatmap without being authenticated (Req 4.7).
/// The aggregator Lambda (task 8.11) will later use these subscriptions to push heatmap deltas.
///
/// Requirements: 4.3, 4.7
/// </summary>
public sealed class SubscribeHeatmapHandler
{
    private const double MinLatitude = -90.0;
    private const double MaxLatitude = 90.0;
    private const double MinLongitude = -180.0;
    private const double MaxLongitude = 180.0;

    private readonly IWsConnectionRepository _wsConnectionRepository;
    private readonly ILogger<SubscribeHeatmapHandler> _logger;

    public SubscribeHeatmapHandler(
        IWsConnectionRepository wsConnectionRepository,
        ILogger<SubscribeHeatmapHandler> logger)
    {
        _wsConnectionRepository = wsConnectionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Processes the subscribe-heatmap message.
    /// </summary>
    /// <param name="connectionId">The API Gateway WebSocket connection ID.</param>
    /// <param name="requestId">The client-generated request ID for correlation.</param>
    /// <param name="data">The raw JSON data element containing bbox coordinates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SubscribeHeatmapResult"/> indicating success or validation failure.</returns>
    public async Task<SubscribeHeatmapResult> HandleAsync(
        string connectionId,
        string requestId,
        JsonElement? data,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Parse bbox from data
        if (data is null || data.Value.ValueKind == JsonValueKind.Null || data.Value.ValueKind == JsonValueKind.Undefined)
        {
            _logger.LogWarning(
                "subscribe-heatmap rejected for connection {ConnectionId}: missing data payload",
                connectionId);
            return SubscribeHeatmapResult.ValidationError("Missing bbox data.");
        }

        var parseResult = ParseBbox(data.Value);
        if (!parseResult.IsValid)
        {
            _logger.LogWarning(
                "subscribe-heatmap rejected for connection {ConnectionId}: {Reason}",
                connectionId, parseResult.ErrorMessage);
            return SubscribeHeatmapResult.ValidationError(parseResult.ErrorMessage!);
        }

        var bbox = parseResult.Bbox!;

        // Step 2: Validate coordinates are within valid ranges
        var validationError = ValidateBbox(bbox);
        if (validationError is not null)
        {
            _logger.LogWarning(
                "subscribe-heatmap rejected for connection {ConnectionId}: {Reason}",
                connectionId, validationError);
            return SubscribeHeatmapResult.ValidationError(validationError);
        }

        // Step 3: Store the bbox subscription on the connection record
        // Format: "minLat,minLng,maxLat,maxLng" for DynamoDB compatibility
        var bboxString = $"{bbox.MinLat},{bbox.MinLng},{bbox.MaxLat},{bbox.MaxLng}";

        await _wsConnectionRepository.UpdateSubscriptionAsync(connectionId, bboxString, cancellationToken);

        _logger.LogInformation(
            "subscribe-heatmap accepted for connection {ConnectionId}: bbox={Bbox}",
            connectionId, bboxString);

        // Step 4: Return success envelope
        return SubscribeHeatmapResult.Success(bboxString);
    }

    /// <summary>
    /// Parses the bbox coordinates from the JSON data element.
    /// Expected format: { "minLat": number, "minLng": number, "maxLat": number, "maxLng": number }
    /// </summary>
    private static BboxParseResult ParseBbox(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            return BboxParseResult.Error("Data must be a JSON object with bbox coordinates.");
        }

        if (!TryGetDouble(data, "minLat", out var minLat))
            return BboxParseResult.Error("Missing or invalid 'minLat' field.");

        if (!TryGetDouble(data, "minLng", out var minLng))
            return BboxParseResult.Error("Missing or invalid 'minLng' field.");

        if (!TryGetDouble(data, "maxLat", out var maxLat))
            return BboxParseResult.Error("Missing or invalid 'maxLat' field.");

        if (!TryGetDouble(data, "maxLng", out var maxLng))
            return BboxParseResult.Error("Missing or invalid 'maxLng' field.");

        return BboxParseResult.Ok(new BboxData(minLat, minLng, maxLat, maxLng));
    }

    /// <summary>
    /// Validates that the bbox coordinates are within valid geographic ranges.
    /// Returns null if valid, or an error message if invalid.
    /// </summary>
    private static string? ValidateBbox(BboxData bbox)
    {
        if (bbox.MinLat < MinLatitude || bbox.MinLat > MaxLatitude)
            return $"minLat must be between {MinLatitude} and {MaxLatitude}.";

        if (bbox.MaxLat < MinLatitude || bbox.MaxLat > MaxLatitude)
            return $"maxLat must be between {MinLatitude} and {MaxLatitude}.";

        if (bbox.MinLng < MinLongitude || bbox.MinLng > MaxLongitude)
            return $"minLng must be between {MinLongitude} and {MaxLongitude}.";

        if (bbox.MaxLng < MinLongitude || bbox.MaxLng > MaxLongitude)
            return $"maxLng must be between {MinLongitude} and {MaxLongitude}.";

        if (bbox.MinLat > bbox.MaxLat)
            return "minLat must be less than or equal to maxLat.";

        if (bbox.MinLng > bbox.MaxLng)
            return "minLng must be less than or equal to maxLng.";

        return null;
    }

    /// <summary>
    /// Attempts to extract a double value from a JSON element by property name.
    /// </summary>
    private static bool TryGetDouble(JsonElement element, string propertyName, out double value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var prop))
            return false;

        if (prop.ValueKind == JsonValueKind.Number)
        {
            value = prop.GetDouble();
            return true;
        }

        return false;
    }
}

/// <summary>
/// Result of the subscribe-heatmap handler.
/// </summary>
public sealed record SubscribeHeatmapResult
{
    /// <summary>Whether the subscription was accepted.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>The stored bbox string on success (format: "minLat,minLng,maxLat,maxLng").</summary>
    public string? Bbox { get; init; }

    /// <summary>Validation error message on failure.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful subscription result.
    /// </summary>
    public static SubscribeHeatmapResult Success(string bbox) => new()
    {
        IsSuccess = true,
        Bbox = bbox
    };

    /// <summary>
    /// Creates a validation error result.
    /// </summary>
    public static SubscribeHeatmapResult ValidationError(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}

/// <summary>
/// Internal DTO for parsed bbox coordinates.
/// </summary>
internal sealed record BboxData(double MinLat, double MinLng, double MaxLat, double MaxLng);

/// <summary>
/// Internal result of bbox parsing.
/// </summary>
internal sealed record BboxParseResult
{
    public bool IsValid { get; init; }
    public BboxData? Bbox { get; init; }
    public string? ErrorMessage { get; init; }

    public static BboxParseResult Ok(BboxData bbox) => new() { IsValid = true, Bbox = bbox };
    public static BboxParseResult Error(string message) => new() { IsValid = false, ErrorMessage = message };
}
