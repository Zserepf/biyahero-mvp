using BiyaHero.Api.Domain;
using BiyaHero.Api.Features.Common.Exceptions;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;
using Route = BiyaHero.Api.Domain.Route;

namespace BiyaHero.Api.Features.Routing.CreateRoute;

/// <summary>
/// Business logic for creating a new community-sourced route (POST /v1/routes).
/// Validates input, extracts user identity from JWT, persists the route with status Unverified.
/// Requirements: 1.1, 1.6, 1.7, 1.8
/// </summary>
public sealed class CreateRouteHandler
{
    // Philippines bounding box per Req 1.8
    private const double MinLatitude = 4.5;
    private const double MaxLatitude = 21.5;
    private const double MinLongitude = 116.0;
    private const double MaxLongitude = 127.0;

    private static readonly HashSet<string> ValidVehicleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(VehicleType.Jeepney),
        nameof(VehicleType.Bus),
        nameof(VehicleType.UV_Express),
        nameof(VehicleType.Tricycle)
    };

    private readonly IRouteRepository _routeRepository;
    private readonly IJwtService _jwtService;

    public CreateRouteHandler(IRouteRepository routeRepository, IJwtService jwtService)
    {
        _routeRepository = routeRepository;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Creates a new route after validating input and authenticating the caller.
    /// Throws UnauthenticatedException for missing/invalid JWT (401).
    /// Throws CreateRouteValidationException for invalid input (422).
    /// </summary>
    public async Task<CreateRouteResponse> HandleAsync(
        string? authorizationHeader,
        CreateRouteRequest request,
        CancellationToken cancellationToken = default)
    {
        // Authenticate — extract user ID from JWT
        var userId = await AuthenticateAsync(authorizationHeader, cancellationToken);

        // Validate input
        Validate(request);

        // Parse vehicle type
        var vehicleType = Enum.Parse<VehicleType>(request.VehicleType, ignoreCase: true);

        // Build domain waypoints from DTOs
        var waypoints = request.Waypoints
            .Select(w => new Waypoint(w.Latitude, w.Longitude, w.SequenceOrder, w.Label))
            .ToList();

        // Create route domain object
        var now = DateTime.UtcNow;
        var route = new Route(
            id: Guid.NewGuid(),
            createdAt: now,
            updatedAt: now,
            name: request.Name,
            vehicleType: vehicleType,
            status: RouteStatus.Verified,
            createdBy: userId,
            baseFare: request.BaseFare,
            waypoints: waypoints);

        // Persist via repository
        await _routeRepository.CreateAsync(route);

        // Return 201 response
        return new CreateRouteResponse(
            Id: route.Id,
            Name: route.Name,
            VehicleType: route.VehicleType.ToString(),
            Status: route.Status.ToString(),
            BaseFare: route.BaseFare,
            WaypointCount: route.Waypoints.Count);
    }

    private async Task<Guid> AuthenticateAsync(string? authorizationHeader, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthenticatedException("Missing or invalid Authorization header.");
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();

        var validationResult = await _jwtService.ValidateTokenDetailedAsync(token, ct);
        if (!validationResult.IsValid)
        {
            throw new UnauthenticatedException(validationResult.ErrorMessage ?? "Invalid token.");
        }

        return validationResult.UserId!.Value;
    }

    private static void Validate(CreateRouteRequest request)
    {
        // Name is required
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new CreateRouteValidationException("Route name is required.");
        }

        // Valid vehicle type
        if (string.IsNullOrWhiteSpace(request.VehicleType) ||
            !ValidVehicleTypes.Contains(request.VehicleType))
        {
            throw new CreateRouteValidationException(
                $"Invalid vehicle type '{request.VehicleType}'. Must be one of: {string.Join(", ", ValidVehicleTypes)}.");
        }

        // At least 2 waypoints
        if (request.Waypoints is null || request.Waypoints.Count < 2)
        {
            throw new CreateRouteValidationException("A route must have at least 2 waypoints.");
        }

        // Validate each waypoint is within Philippines bounding box
        for (var i = 0; i < request.Waypoints.Count; i++)
        {
            var wp = request.Waypoints[i];

            if (wp.Latitude < MinLatitude || wp.Latitude > MaxLatitude)
            {
                throw new CreateRouteValidationException(
                    $"Waypoint {i} latitude {wp.Latitude} is outside the valid range ({MinLatitude}° to {MaxLatitude}° N).");
            }

            if (wp.Longitude < MinLongitude || wp.Longitude > MaxLongitude)
            {
                throw new CreateRouteValidationException(
                    $"Waypoint {i} longitude {wp.Longitude} is outside the valid range ({MinLongitude}° to {MaxLongitude}° E).");
            }
        }
    }
}
