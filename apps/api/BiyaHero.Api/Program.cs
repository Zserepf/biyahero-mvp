using System.Text.Json.Serialization;
using Amazon.CloudWatch;
using Amazon.DynamoDBv2;
using Amazon.SimpleEmail;
using BiyaHero.Api.Features.Admin.ListUsers;
using BiyaHero.Api.Features.Admin.PromoteUser;
using BiyaHero.Api.Features.Admin.SuspendUser;
using BiyaHero.Api.Features.Auth.Login;
using BiyaHero.Api.Features.Auth.Logout;
using BiyaHero.Api.Features.Auth.Me;
using BiyaHero.Api.Features.Auth.Refresh;
using BiyaHero.Api.Features.Auth.Register;
using BiyaHero.Api.Features.Auth.VerifyEmail;
using BiyaHero.Api.Features.Common;
using BiyaHero.Api.Features.Common.FreeTierMonitor;
using BiyaHero.Api.Features.Health;
using BiyaHero.Api.Features.Fare;
using BiyaHero.Api.Features.Heatmap.Aggregator;
using BiyaHero.Api.Features.Heatmap.GetTiles;
using BiyaHero.Api.Features.I18n;
using BiyaHero.Api.Features.Payment.AudioFailures;
using BiyaHero.Api.Features.Payment;
using BiyaHero.Api.Features.Payment.Webhook;
using BiyaHero.Api.Features.Routing.ApproveRevision;
using BiyaHero.Api.Features.Routing.CreateRoute;
using BiyaHero.Api.Features.Routing.GetRoute;
using BiyaHero.Api.Features.Routing.ListRoutes;
using BiyaHero.Api.Features.Routing.SubmitRevision;
using BiyaHero.Api.Features.Routing.VoteRoute;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;
using BiyaHero.Api.WebSockets;

using RouteListItemDto = BiyaHero.Api.Features.Routing.ListRoutes.RouteListItemDto;
using RouteDetailDto = BiyaHero.Api.Features.Routing.GetRoute.RouteDetailDto;
using WaypointDto = BiyaHero.Api.Features.Routing.GetRoute.WaypointDto;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// Register PostgreSQL repository infrastructure
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Database=biyahero;Username=postgres;Password=postgres";
builder.Services.AddPostgresRepositories(connectionString);

// Register DynamoDB client and repositories
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var config = new AmazonDynamoDBConfig
    {
        RegionEndpoint = Amazon.RegionEndpoint.APSoutheast1
    };
    return new AmazonDynamoDBClient(config);
});
builder.Services.AddScoped<IPaymentEventRepository, PaymentEventRepository>();
builder.Services.AddScoped<IWsConnectionRepository, WsConnectionRepository>();
builder.Services.AddScoped<IQueuedMessageRepository, QueuedMessageRepository>();
builder.Services.AddScoped<IDemandPingRepository, DemandPingRepository>();

// Register CloudWatch client and FreeTierMonitor
builder.Services.AddSingleton<IAmazonCloudWatch>(sp =>
{
    var config = new AmazonCloudWatchConfig
    {
        RegionEndpoint = Amazon.RegionEndpoint.APSoutheast1
    };
    return new AmazonCloudWatchClient(config);
});
builder.Services.AddScoped<FreeTierMonitorHandler>();

// Register services
builder.Services.AddSingleton<ISecretService, ConfigSecretService>();
builder.Services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton(TimeProvider.System);

// Register Fare services
builder.Services.AddFareServices();
builder.Services.AddScoped<FareCalculateHandler>();

// Register Auth services
builder.Services.AddSingleton<IVerificationTokenStore, InMemoryVerificationTokenStore>();
builder.Services.AddSingleton<IAmazonSimpleEmailService>(sp =>
    new AmazonSimpleEmailServiceClient(Amazon.RegionEndpoint.APSoutheast1));
builder.Services.AddSingleton<IEmailService, SesEmailService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddScoped<IRouteVoteRepository, RouteVoteRepository>();
builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<VerifyEmailHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<LogoutHandler>();
builder.Services.AddScoped<MeHandler>();
builder.Services.AddScoped<RefreshHandler>();

// Register Heatmap handlers
builder.Services.AddScoped<GetTilesHandler>();
builder.Services.AddScoped<HeatmapAggregatorHandler>();

// Register Payment services
builder.Services.AddSingleton<BiyaHero.Api.Services.IWalletAdapter, BiyaHero.Api.Services.MockWalletAdapter>();
builder.Services.AddSingleton<IWebhookSignatureVerifier, WebhookSignatureVerifier>();
builder.Services.AddSingleton<IWebSocketPushService, ApiGatewayWebSocketPushService>();
builder.Services.AddScoped<WebhookHandler>();

// Register WebSocket handlers
builder.Services.AddScoped<ConnectHandler>();

// Register Audit Log services
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// Register Admin handlers
builder.Services.AddScoped<ListUsersHandler>();
builder.Services.AddScoped<SuspendUserHandler>();
builder.Services.AddScoped<PromoteUserHandler>();

// Register Routing handlers
builder.Services.AddScoped<ListRoutesHandler>();
builder.Services.AddScoped<GetRouteHandler>();
builder.Services.AddScoped<CreateRouteHandler>();
builder.Services.AddScoped<ApproveRevisionHandler>();
builder.Services.AddScoped<SubmitRevisionHandler>();
builder.Services.AddSingleton<IRouteRevisionRepository, InMemoryRouteRevisionRepository>();
builder.Services.AddScoped<VoteRouteHandler>();

// Register WebSocket handlers
builder.Services.AddScoped<DisconnectHandler>();
builder.Services.AddScoped<SubscribeHeatmapHandler>();
builder.Services.AddScoped<CancelDemandHandler>();
builder.Services.AddScoped<DemandPingHandler>();

// Register Health check handler
builder.Services.AddScoped<HealthCheckHandler>();

var app = builder.Build();

// Global exception handling — translates BiyaHeroException to standard error envelope
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapHealthCheckEndpoint();

// Auth endpoints
app.MapRegisterEndpoint();
app.MapVerifyEmailEndpoint();
app.MapLoginEndpoint();
app.MapLogoutEndpoint();
app.MapRefreshEndpoint();
app.MapMeEndpoint();
app.MapUpdateLanguagePreferenceEndpoint();

// I18n endpoints
app.MapMissingKeysEndpoint();

// Payment endpoints
app.MapAudioFailureEndpoint();
app.MapWebhookEndpoint();

// Fare endpoints
app.MapFareCalculateEndpoint();

// Routing endpoints
app.MapListRoutesEndpoint();
app.MapGetRouteEndpoint();
app.MapCreateRouteEndpoint();
app.MapApproveRevisionEndpoint();
app.MapSubmitRevisionEndpoint();
app.MapVoteRouteEndpoint();

// Heatmap endpoints
app.MapGetTilesEndpoint();

// Admin endpoints
app.MapListUsersEndpoint();
app.MapSuspendUserEndpoint();
app.MapPromoteUserEndpoint();

app.Run();

[JsonSerializable(typeof(HealthCheckResponse))]
[JsonSerializable(typeof(DependencyStatus))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(RegisterResponse))]
[JsonSerializable(typeof(VerifyEmailRequest))]
[JsonSerializable(typeof(VerifyEmailResponse))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(RefreshRequest))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(MeResponse))]
[JsonSerializable(typeof(UpdateLanguagePreferenceRequest))]
[JsonSerializable(typeof(UpdateLanguagePreferenceResponse))]
[JsonSerializable(typeof(MissingKeysRequest))]
[JsonSerializable(typeof(MissingKeyEntry))]
[JsonSerializable(typeof(List<MissingKeyEntry>))]
[JsonSerializable(typeof(FareCalculateRequest))]
[JsonSerializable(typeof(FareCalculateResponse))]
[JsonSerializable(typeof(RouteListItemDto))]
[JsonSerializable(typeof(List<RouteListItemDto>))]
[JsonSerializable(typeof(RouteDetailDto))]
[JsonSerializable(typeof(WaypointDto))]
[JsonSerializable(typeof(List<WaypointDto>))]
[JsonSerializable(typeof(CreateRouteRequest))]
[JsonSerializable(typeof(CreateRouteResponse))]
[JsonSerializable(typeof(CreateRouteWaypointDto))]
[JsonSerializable(typeof(List<CreateRouteWaypointDto>))]
[JsonSerializable(typeof(SubmitRevisionRequest))]
[JsonSerializable(typeof(SubmitRevisionResponse))]
[JsonSerializable(typeof(SubmitRevisionWaypointDto))]
[JsonSerializable(typeof(List<SubmitRevisionWaypointDto>))]
[JsonSerializable(typeof(VoteRouteRequest))]
[JsonSerializable(typeof(VoteRouteResponse))]
[JsonSerializable(typeof(ApproveRevisionResponse))]
[JsonSerializable(typeof(ApproveRevisionRouteDto))]
[JsonSerializable(typeof(ApproveRevisionWaypointDto))]
[JsonSerializable(typeof(List<ApproveRevisionWaypointDto>))]
[JsonSerializable(typeof(HeatmapTileDto))]
[JsonSerializable(typeof(List<HeatmapTileDto>))]
[JsonSerializable(typeof(AudioFailureRequest))]
[JsonSerializable(typeof(WebhookRequest))]
[JsonSerializable(typeof(WebhookResponse))]
[JsonSerializable(typeof(ListUsersItemDto))]
[JsonSerializable(typeof(List<ListUsersItemDto>))]
[JsonSerializable(typeof(SuspendUserResponse))]
[JsonSerializable(typeof(PromoteUserRequest))]
[JsonSerializable(typeof(PromoteUserResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
