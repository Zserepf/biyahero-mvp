using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.KMS;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.RDS;
using Amazon.CDK.AWS.SES;
using Constructs;

namespace BiyaHero.Infra;

/// <summary>
/// Properties passed from DataStack to ApiStack for cross-stack references.
/// </summary>
public class ApiStackProps : StackProps
{
    public required DatabaseInstance RdsInstance { get; init; }
    public required IVpc Vpc { get; init; }
    public required ISecurityGroup DatabaseSecurityGroup { get; init; }
    public required Table DemandPingsTable { get; init; }
    public required Table PaymentEventsTable { get; init; }
    public required Table WsConnectionsTable { get; init; }
    public required Table QueuedMessagesTable { get; init; }
    public required Key JwtSigningKey { get; init; }
    public required Key WebhookSigningKey { get; init; }
}

/// <summary>
/// API Gateway REST + WebSocket APIs, Lambda functions per handler,
/// IAM roles, SES identity for verification email, and EventBridge rule
/// for the 5-second heatmap aggregator cadence.
/// Requirements: 2.9, 3.2, 4.2, 4.3, 5.1, 7.1, 7.2, 7.3
/// </summary>
public class ApiStack : Stack
{
    /// <summary>REST API endpoint URL (exported for frontend config).</summary>
    public string RestApiUrl { get; }

    /// <summary>WebSocket API endpoint URL (exported for frontend config).</summary>
    public string WebSocketApiUrl { get; }

    internal ApiStack(Construct scope, string id, ApiStackProps props)
        : base(scope, id, props)
    {
        // ─── Common Lambda environment variables ────────────────────────────────
        var commonEnv = new Dictionary<string, string>
        {
            ["DB_HOST"] = props.RdsInstance.DbInstanceEndpointAddress,
            ["DB_PORT"] = props.RdsInstance.DbInstanceEndpointPort,
            ["DB_NAME"] = "biyahero",
            ["DB_SECRET_ARN"] = props.RdsInstance.Secret!.SecretArn,
            ["DEMAND_PINGS_TABLE"] = props.DemandPingsTable.TableName,
            ["PAYMENT_EVENTS_TABLE"] = props.PaymentEventsTable.TableName,
            ["WS_CONNECTIONS_TABLE"] = props.WsConnectionsTable.TableName,
            ["QUEUED_MESSAGES_TABLE"] = props.QueuedMessagesTable.TableName,
            ["JWT_KEY_ID"] = props.JwtSigningKey.KeyId,
            ["WEBHOOK_KEY_ID"] = props.WebhookSigningKey.KeyId
        };

        // ─── Security Group for Lambda functions accessing RDS ──────────────────
        var lambdaSecurityGroup = new SecurityGroup(this, "LambdaSecurityGroup", new SecurityGroupProps
        {
            Vpc = props.Vpc,
            Description = "Security group for BiyaHero Lambda functions",
            AllowAllOutbound = true
        });

        // Allow Lambda SG to connect to RDS SG on port 5432
        props.DatabaseSecurityGroup.AddIngressRule(
            lambdaSecurityGroup,
            Port.Tcp(5432),
            "Allow Lambda functions to access RDS PostgreSQL"
        );

        // ─── SES Email Identity ────────────────────────────────────────────────
        // Verification email sender identity (Req 5.1)
        var sesIdentity = new CfnEmailIdentity(this, "BiyaHeroSesIdentity", new CfnEmailIdentityProps
        {
            EmailIdentity = "noreply@biyahero.app"
        });

        // ─── REST API Gateway ──────────────────────────────────────────────────
        // Req 7.1, 7.2: REST API with throttling to stay within free tier
        var restApi = new RestApi(this, "BiyaHeroRestApi", new RestApiProps
        {
            RestApiName = "BiyaHero-REST",
            Description = "BiyaHero MVP REST API (v1)",
            DeployOptions = new StageOptions
            {
                StageName = "prod",
                ThrottlingRateLimit = 100,
                ThrottlingBurstLimit = 200
            },
            DefaultCorsPreflightOptions = new CorsOptions
            {
                AllowOrigins = Cors.ALL_ORIGINS,
                AllowMethods = Cors.ALL_METHODS,
                AllowHeaders = new[]
                {
                    "Content-Type",
                    "Authorization",
                    "X-Wallet-Signature",
                    "X-Wallet-Timestamp"
                }
            }
        });

        RestApiUrl = restApi.Url;

        // ─── WebSocket API Gateway ─────────────────────────────────────────────
        // Req 7.3: WebSocket API for real-time flows (heatmap, payment confirmations)
        var webSocketApi = new CfnApi(this, "BiyaHeroWebSocketApi", new CfnApiProps
        {
            Name = "BiyaHero-WebSocket",
            ProtocolType = "WEBSOCKET",
            RouteSelectionExpression = "$request.body.action"
        });

        var wsStage = new CfnStage(this, "BiyaHeroWsStage", new CfnStageProps
        {
            ApiId = webSocketApi.Ref,
            StageName = "prod",
            AutoDeploy = true
        });

        WebSocketApiUrl = $"wss://{webSocketApi.Ref}.execute-api.{this.Region}.amazonaws.com/prod";

        // WebSocket management endpoint for PostToConnection calls
        var wsManagementUrl = $"https://{webSocketApi.Ref}.execute-api.{this.Region}.amazonaws.com/prod";

        // ─── Lambda Base Policy (CloudWatch Logs) ──────────────────────────────
        var lambdaBasePolicy = new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[]
            {
                "logs:CreateLogGroup",
                "logs:CreateLogStream",
                "logs:PutLogEvents"
            },
            Resources = new[] { "*" }
        });

        // Policy for reading RDS credentials from Secrets Manager
        var dbSecretPolicy = new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "secretsmanager:GetSecretValue" },
            Resources = new[] { props.RdsInstance.Secret!.SecretArn }
        });

        // Policy for PostToConnection on WebSocket API
        var wsManagePolicy = new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "execute-api:ManageConnections" },
            Resources = new[]
            {
                $"arn:aws:execute-api:{this.Region}:{this.Account}:{webSocketApi.Ref}/prod/POST/@connections/*"
            }
        });

        // ─── Auth Lambda ───────────────────────────────────────────────────────
        // Handles: registrations, email verification, login, refresh, logout,
        //          me, language-preference, i18n/missing-keys, admin endpoints, health
        var authLambda = CreateLambda("AuthHandler", "Auth", commonEnv, props.Vpc, lambdaSecurityGroup);
        authLambda.AddToRolePolicy(lambdaBasePolicy);
        authLambda.AddToRolePolicy(dbSecretPolicy);
        props.JwtSigningKey.GrantEncryptDecrypt(authLambda);
        authLambda.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "ses:SendEmail", "ses:SendRawEmail" },
            Resources = new[] { $"arn:aws:ses:{this.Region}:{this.Account}:identity/noreply@biyahero.app" }
        }));
        authLambda.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "logs:PutLogEvents", "logs:CreateLogStream" },
            Resources = new[] { $"arn:aws:logs:{this.Region}:{this.Account}:log-group:/biyahero/audit:*" }
        }));
        authLambda.AddEnvironment("SES_SENDER_EMAIL", "noreply@biyahero.app");
        authLambda.AddEnvironment("AUDIT_LOG_GROUP", "/biyahero/audit");

        // ─── Routing Lambda ────────────────────────────────────────────────────
        // Handles: route CRUD, revisions, votes, bbox queries
        var routingLambda = CreateLambda("RoutingHandler", "Routing", commonEnv, props.Vpc, lambdaSecurityGroup);
        routingLambda.AddToRolePolicy(lambdaBasePolicy);
        routingLambda.AddToRolePolicy(dbSecretPolicy);
        props.JwtSigningKey.GrantDecrypt(routingLambda);

        // ─── Fare Lambda ───────────────────────────────────────────────────────
        // Handles: fare calculation (Req 2.9 — p95 ≤ 200ms)
        var fareLambda = CreateLambda("FareHandler", "Fare", commonEnv, props.Vpc, lambdaSecurityGroup);
        fareLambda.AddToRolePolicy(lambdaBasePolicy);
        fareLambda.AddToRolePolicy(dbSecretPolicy);

        // ─── Payment Lambda ────────────────────────────────────────────────────
        // Handles: webhook ingestion, audio-failure logging (Req 3.2)
        var paymentLambda = CreateLambda("PaymentHandler", "Payment", commonEnv, props.Vpc, lambdaSecurityGroup);
        paymentLambda.AddToRolePolicy(lambdaBasePolicy);
        props.PaymentEventsTable.GrantReadWriteData(paymentLambda);
        props.WsConnectionsTable.GrantReadData(paymentLambda);
        props.QueuedMessagesTable.GrantReadWriteData(paymentLambda);
        props.WebhookSigningKey.GrantDecrypt(paymentLambda);
        paymentLambda.AddToRolePolicy(wsManagePolicy);
        paymentLambda.AddEnvironment("WS_API_ENDPOINT", wsManagementUrl);
        paymentLambda.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "logs:PutLogEvents", "logs:CreateLogStream" },
            Resources = new[] { $"arn:aws:logs:{this.Region}:{this.Account}:log-group:/biyahero/audit:*" }
        }));

        // ─── Heatmap Lambda ────────────────────────────────────────────────────
        // Handles: GET /v1/heatmap/tiles (Req 4.2 — p95 ≤ 500ms)
        var heatmapLambda = CreateLambda("HeatmapHandler", "Heatmap", commonEnv, props.Vpc, lambdaSecurityGroup);
        heatmapLambda.AddToRolePolicy(lambdaBasePolicy);
        props.DemandPingsTable.GrantReadData(heatmapLambda);

        // ─── WebSocket $connect Handler ────────────────────────────────────────
        // Validates JWT, registers connection, drains queued messages (Req 5.4, 3.6)
        var wsConnectLambda = CreateLambda("WsConnectHandler", "WebSockets.Connect", commonEnv, props.Vpc, lambdaSecurityGroup);
        wsConnectLambda.AddToRolePolicy(lambdaBasePolicy);
        props.WsConnectionsTable.GrantReadWriteData(wsConnectLambda);
        props.QueuedMessagesTable.GrantReadWriteData(wsConnectLambda);
        props.JwtSigningKey.GrantDecrypt(wsConnectLambda);
        wsConnectLambda.AddToRolePolicy(wsManagePolicy);
        wsConnectLambda.AddEnvironment("WS_API_ENDPOINT", wsManagementUrl);

        // ─── WebSocket $disconnect Handler ─────────────────────────────────────
        // Removes connection record from WsConnections
        var wsDisconnectLambda = CreateLambda("WsDisconnectHandler", "WebSockets.Disconnect", commonEnv, props.Vpc, lambdaSecurityGroup);
        wsDisconnectLambda.AddToRolePolicy(lambdaBasePolicy);
        props.WsConnectionsTable.GrantReadWriteData(wsDisconnectLambda);

        // ─── WebSocket Route Handler ───────────────────────────────────────────
        // Handles: demand-ping, cancel-demand, subscribe-heatmap, ping (Req 4.1, 4.3, 4.5, 4.7)
        var wsRouteLambda = CreateLambda("WsRouteHandler", "WebSockets.Route", commonEnv, props.Vpc, lambdaSecurityGroup);
        wsRouteLambda.AddToRolePolicy(lambdaBasePolicy);
        props.DemandPingsTable.GrantReadWriteData(wsRouteLambda);
        props.WsConnectionsTable.GrantReadWriteData(wsRouteLambda);
        props.JwtSigningKey.GrantDecrypt(wsRouteLambda);
        wsRouteLambda.AddToRolePolicy(wsManagePolicy);
        wsRouteLambda.AddEnvironment("WS_API_ENDPOINT", wsManagementUrl);

        // ─── Heatmap Aggregator Lambda (EventBridge-triggered) ─────────────────
        // Req 4.3: Push heatmap deltas every 5 seconds to subscribed drivers
        var aggregatorLambda = CreateLambda("HeatmapAggregator", "Heatmap.Aggregator", commonEnv, props.Vpc, lambdaSecurityGroup);
        aggregatorLambda.AddToRolePolicy(lambdaBasePolicy);
        props.DemandPingsTable.GrantReadData(aggregatorLambda);
        props.WsConnectionsTable.GrantReadData(aggregatorLambda);
        aggregatorLambda.AddToRolePolicy(wsManagePolicy);
        aggregatorLambda.AddEnvironment("WS_API_ENDPOINT", wsManagementUrl);

        // ─── REST API Routes ───────────────────────────────────────────────────
        var v1 = restApi.Root.AddResource("v1");

        // --- Auth routes ---
        var auth = v1.AddResource("auth");
        var registrations = auth.AddResource("registrations");
        registrations.AddMethod("POST", new LambdaIntegration(authLambda));

        var emailVerifications = auth.AddResource("email-verifications");
        var verifyAction = emailVerifications.AddResource(":verify");
        verifyAction.AddMethod("POST", new LambdaIntegration(authLambda));

        var sessions = auth.AddResource("sessions");
        sessions.AddMethod("POST", new LambdaIntegration(authLambda));
        var sessionRefresh = sessions.AddResource(":refresh");
        sessionRefresh.AddMethod("POST", new LambdaIntegration(authLambda));
        var sessionById = sessions.AddResource("{sessionId}");
        sessionById.AddMethod("DELETE", new LambdaIntegration(authLambda));

        var me = auth.AddResource("me");
        me.AddMethod("GET", new LambdaIntegration(authLambda));
        var langPref = me.AddResource("language-preference");
        langPref.AddMethod("PATCH", new LambdaIntegration(authLambda));

        // --- i18n routes ---
        var i18n = v1.AddResource("i18n");
        var missingKeys = i18n.AddResource("missing-keys");
        missingKeys.AddMethod("POST", new LambdaIntegration(authLambda));

        // --- Routing routes ---
        var routes = v1.AddResource("routes");
        routes.AddMethod("GET", new LambdaIntegration(routingLambda));
        routes.AddMethod("POST", new LambdaIntegration(routingLambda));
        var routeById = routes.AddResource("{routeId}");
        routeById.AddMethod("GET", new LambdaIntegration(routingLambda));
        var revisions = routeById.AddResource("revisions");
        revisions.AddMethod("POST", new LambdaIntegration(routingLambda));
        var revisionById = revisions.AddResource("{revisionId}");
        var approveAction = revisionById.AddResource(":approve");
        approveAction.AddMethod("POST", new LambdaIntegration(routingLambda));
        var votes = routeById.AddResource("votes");
        votes.AddMethod("POST", new LambdaIntegration(routingLambda));

        // --- Fare routes ---
        var fare = v1.AddResource("fare");
        var fareCalculate = fare.AddResource(":calculate");
        fareCalculate.AddMethod("POST", new LambdaIntegration(fareLambda));

        // --- Payment routes ---
        var payments = v1.AddResource("payments");
        var webhook = payments.AddResource("webhook");
        webhook.AddMethod("POST", new LambdaIntegration(paymentLambda));
        var audioFailures = payments.AddResource("audio-failures");
        audioFailures.AddMethod("POST", new LambdaIntegration(paymentLambda));

        // --- Heatmap routes ---
        var heatmap = v1.AddResource("heatmap");
        var tiles = heatmap.AddResource("tiles");
        tiles.AddMethod("GET", new LambdaIntegration(heatmapLambda));

        // --- Health route ---
        var health = v1.AddResource("health");
        health.AddMethod("GET", new LambdaIntegration(authLambda));

        // --- Admin routes ---
        var admin = v1.AddResource("admin");
        var adminUsers = admin.AddResource("users");
        adminUsers.AddMethod("GET", new LambdaIntegration(authLambda));
        var adminUserById = adminUsers.AddResource("{userId}");
        var suspendAction = adminUserById.AddResource(":suspend");
        suspendAction.AddMethod("POST", new LambdaIntegration(authLambda));
        var promoteAction = adminUserById.AddResource(":promote");
        promoteAction.AddMethod("POST", new LambdaIntegration(authLambda));

        // ─── WebSocket API Route Integrations ──────────────────────────────────
        CreateWebSocketRoute(webSocketApi, "$connect", wsConnectLambda);
        CreateWebSocketRoute(webSocketApi, "$disconnect", wsDisconnectLambda);
        CreateWebSocketRoute(webSocketApi, "$default", wsRouteLambda);
        CreateWebSocketRoute(webSocketApi, "demand-ping", wsRouteLambda);
        CreateWebSocketRoute(webSocketApi, "cancel-demand", wsRouteLambda);
        CreateWebSocketRoute(webSocketApi, "subscribe-heatmap", wsRouteLambda);
        CreateWebSocketRoute(webSocketApi, "ping", wsRouteLambda);

        // Grant API Gateway permission to invoke each WebSocket Lambda
        var wsApiArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{webSocketApi.Ref}/*";
        wsConnectLambda.AddPermission("WsApiInvokeConnect", new Permission
        {
            Principal = new ServicePrincipal("apigateway.amazonaws.com"),
            SourceArn = wsApiArn
        });
        wsDisconnectLambda.AddPermission("WsApiInvokeDisconnect", new Permission
        {
            Principal = new ServicePrincipal("apigateway.amazonaws.com"),
            SourceArn = wsApiArn
        });
        wsRouteLambda.AddPermission("WsApiInvokeRoute", new Permission
        {
            Principal = new ServicePrincipal("apigateway.amazonaws.com"),
            SourceArn = wsApiArn
        });

        // ─── EventBridge Rule: 5-second Heatmap Aggregator Cadence ─────────────
        // Req 4.3: Push updated Heatmap_Tile aggregates at intervals ≤ 5 seconds
        var aggregatorRule = new Rule(this, "HeatmapAggregatorSchedule", new RuleProps
        {
            RuleName = "BiyaHero-HeatmapAggregator-5s",
            Description = "Triggers heatmap aggregation every 5 seconds (Req 4.3)",
            Schedule = Schedule.Rate(Duration.Seconds(5))
        });

        aggregatorRule.AddTarget(new LambdaFunction(aggregatorLambda));

        // ─── Stack Outputs ─────────────────────────────────────────────────────
        _ = new CfnOutput(this, "RestApiEndpoint", new CfnOutputProps
        {
            Value = restApi.Url,
            Description = "BiyaHero REST API endpoint",
            ExportName = "BiyaHero-RestApiEndpoint"
        });

        _ = new CfnOutput(this, "WebSocketApiEndpoint", new CfnOutputProps
        {
            Value = WebSocketApiUrl,
            Description = "BiyaHero WebSocket API endpoint",
            ExportName = "BiyaHero-WebSocketApiEndpoint"
        });

        _ = new CfnOutput(this, "WebSocketApiId", new CfnOutputProps
        {
            Value = webSocketApi.Ref,
            Description = "WebSocket API ID for management endpoint construction",
            ExportName = "BiyaHero-WebSocketApiId"
        });
    }

    /// <summary>
    /// Creates a .NET 8 AOT Lambda function with standard configuration.
    /// ARM64 architecture for cost efficiency and free-tier alignment.
    /// 256 MB memory, 30s timeout, X-Ray tracing enabled.
    /// </summary>
    private Function CreateLambda(
        string id,
        string handlerNamespace,
        Dictionary<string, string> environment,
        IVpc vpc,
        ISecurityGroup securityGroup)
    {
        var fn = new Function(this, id, new FunctionProps
        {
            FunctionName = $"BiyaHero-{id}",
            Runtime = Runtime.DOTNET_8,
            Architecture = Architecture.ARM_64,
            Handler = $"BiyaHero.Api::BiyaHero.Api.Features.{handlerNamespace}.Handler::FunctionHandler",
            Code = Code.FromAsset("../apps/api/BiyaHero.Api/bin/Release/net8.0/linux-arm64/publish"),
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>(environment),
            Tracing = Tracing.ACTIVE,
            Vpc = vpc,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
            SecurityGroups = new[] { securityGroup }
        });

        return fn;
    }

    /// <summary>
    /// Creates a WebSocket API route with Lambda proxy integration.
    /// </summary>
    private void CreateWebSocketRoute(CfnApi webSocketApi, string routeKey, Function handler)
    {
        // Sanitize route key for construct IDs (remove $ and - characters)
        var sanitized = routeKey.Replace("$", "").Replace("-", "");
        var integrationId = $"{sanitized}Integration";
        var routeId = $"{sanitized}Route";

        var integration = new CfnIntegration(this, integrationId, new CfnIntegrationProps
        {
            ApiId = webSocketApi.Ref,
            IntegrationType = "AWS_PROXY",
            IntegrationUri = $"arn:aws:apigateway:{this.Region}:lambda:path/2015-03-31/functions/{handler.FunctionArn}/invocations"
        });

        _ = new CfnRoute(this, routeId, new CfnRouteProps
        {
            ApiId = webSocketApi.Ref,
            RouteKey = routeKey,
            AuthorizationType = "NONE",
            Target = $"integrations/{integration.Ref}"
        });
    }
}
