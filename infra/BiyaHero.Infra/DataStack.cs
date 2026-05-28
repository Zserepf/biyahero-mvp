using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.KMS;
using Amazon.CDK.AWS.RDS;
using Constructs;

namespace BiyaHero.Infra;

/// <summary>
/// RDS PostgreSQL with PostGIS, DynamoDB tables (DemandPings, PaymentEvents,
/// WsConnections, QueuedMessages), and KMS keys for JWT and webhook signing.
/// </summary>
public class DataStack : Stack
{
    /// <summary>The RDS PostgreSQL database instance.</summary>
    public DatabaseInstance Database { get; }

    /// <summary>The VPC hosting the RDS instance.</summary>
    public IVpc Vpc { get; }

    /// <summary>Security group for the RDS instance.</summary>
    public ISecurityGroup DatabaseSecurityGroup { get; }

    /// <summary>DynamoDB table for demand pings (heatmap).</summary>
    public Table DemandPingsTable { get; }

    /// <summary>DynamoDB table for payment events.</summary>
    public Table PaymentEventsTable { get; }

    /// <summary>DynamoDB table for WebSocket connections.</summary>
    public Table WsConnectionsTable { get; }

    /// <summary>DynamoDB table for queued offline messages.</summary>
    public Table QueuedMessagesTable { get; }

    /// <summary>KMS key used for JWT signing.</summary>
    public Key JwtSigningKey { get; }

    /// <summary>KMS key used for webhook signature verification.</summary>
    public Key WebhookSigningKey { get; }

    internal DataStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // ─────────────────────────────────────────────────────────────────────
        // VPC — minimal 2-AZ setup for RDS (free-tier eligible, no NAT gateway)
        // ─────────────────────────────────────────────────────────────────────
        Vpc = new Vpc(this, "BiyaHeroVpc", new VpcProps
        {
            MaxAzs = 2,
            NatGateways = 0, // Avoid NAT costs on free tier
            SubnetConfiguration = new ISubnetConfiguration[]
            {
                new SubnetConfiguration
                {
                    Name = "Public",
                    SubnetType = SubnetType.PUBLIC,
                    CidrMask = 24
                },
                new SubnetConfiguration
                {
                    Name = "Isolated",
                    SubnetType = SubnetType.PRIVATE_ISOLATED,
                    CidrMask = 24
                }
            }
        });

        // ─────────────────────────────────────────────────────────────────────
        // RDS PostgreSQL t4g.micro with PostGIS
        // Free tier: 750 hours/month for 12 months, 20 GB gp3 storage
        // ─────────────────────────────────────────────────────────────────────
        DatabaseSecurityGroup = new SecurityGroup(this, "DbSecurityGroup", new SecurityGroupProps
        {
            Vpc = Vpc,
            Description = "Security group for BiyaHero RDS PostgreSQL",
            AllowAllOutbound = false
        });

        // Allow inbound PostgreSQL from within the VPC
        DatabaseSecurityGroup.AddIngressRule(
            Peer.Ipv4(Vpc.VpcCidrBlock),
            Port.Tcp(5432),
            "Allow PostgreSQL access from within VPC"
        );

        Database = new DatabaseInstance(this, "BiyaHeroDb", new DatabaseInstanceProps
        {
            Engine = DatabaseInstanceEngine.Postgres(new PostgresInstanceEngineProps
            {
                Version = PostgresEngineVersion.VER_16_4
            }),
            InstanceType = InstanceType.Of(InstanceClass.BURSTABLE4_GRAVITON, InstanceSize.MICRO),
            Vpc = Vpc,
            VpcSubnets = new SubnetSelection
            {
                SubnetType = SubnetType.PRIVATE_ISOLATED
            },
            SecurityGroups = new ISecurityGroup[] { DatabaseSecurityGroup },
            DatabaseName = "biyahero",
            Credentials = Credentials.FromGeneratedSecret("biyahero_admin", new CredentialsBaseOptions
            {
                SecretName = "biyahero/db-credentials"
            }),
            AllocatedStorage = 20, // 20 GB gp3 — free tier limit
            StorageType = StorageType.GP3,
            MultiAz = false, // Single AZ for free tier
            PubliclyAccessible = false,
            DeletionProtection = false, // MVP — allow teardown
            RemovalPolicy = RemovalPolicy.DESTROY,
            BackupRetention = Duration.Days(7),
            // PostGIS is enabled via SQL migration (CREATE EXTENSION postgis)
            // after the instance is provisioned
            Parameters = new Dictionary<string, string>
            {
                // Ensure shared_preload_libraries is empty or default;
                // PostGIS extension is loaded on-demand via CREATE EXTENSION
            }
        });

        // ─────────────────────────────────────────────────────────────────────
        // DynamoDB Tables — on-demand capacity, free tier perpetual
        // (25 WCU + 25 RCU always-free baseline, 25 GB storage)
        // ─────────────────────────────────────────────────────────────────────

        // --- DemandPings table ---
        // PK: GEOHASH#{geohash5}, SK: PING#{commuterId}#{pingId}
        // GSI byCommuterId: PK commuterId → fast cancel
        // TTL: expiresAt (5 minutes after submission)
        DemandPingsTable = new Table(this, "DemandPings", new TableProps
        {
            TableName = "BiyaHero-DemandPings",
            PartitionKey = new Attribute { Name = "pk", Type = AttributeType.STRING },
            SortKey = new Attribute { Name = "sk", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            TimeToLiveAttribute = "expiresAt",
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        DemandPingsTable.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
        {
            IndexName = "byCommuterId",
            PartitionKey = new Attribute { Name = "commuterId", Type = AttributeType.STRING },
            ProjectionType = ProjectionType.ALL
        });

        // --- PaymentEvents table ---
        // PK: EVENT#{eventId}, SK: EVENT#{eventId} (single-item pattern)
        // GSI byDriverId: PK driverId, SK occurredAt → driver dashboard history
        // TTL: expiresAt (90 days per Req 8.2)
        PaymentEventsTable = new Table(this, "PaymentEvents", new TableProps
        {
            TableName = "BiyaHero-PaymentEvents",
            PartitionKey = new Attribute { Name = "pk", Type = AttributeType.STRING },
            SortKey = new Attribute { Name = "sk", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            TimeToLiveAttribute = "expiresAt",
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        PaymentEventsTable.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
        {
            IndexName = "byDriverId",
            PartitionKey = new Attribute { Name = "driverId", Type = AttributeType.STRING },
            SortKey = new Attribute { Name = "occurredAt", Type = AttributeType.STRING },
            ProjectionType = ProjectionType.ALL
        });

        // --- WsConnections table ---
        // PK: USER#{userId}, SK: CONN#{connectionId}
        // GSI byConnectionId: PK connectionId → fast $disconnect cleanup
        // TTL: expiresAt (24h safety net for orphaned connections)
        WsConnectionsTable = new Table(this, "WsConnections", new TableProps
        {
            TableName = "BiyaHero-WsConnections",
            PartitionKey = new Attribute { Name = "pk", Type = AttributeType.STRING },
            SortKey = new Attribute { Name = "sk", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            TimeToLiveAttribute = "expiresAt",
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        WsConnectionsTable.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
        {
            IndexName = "byConnectionId",
            PartitionKey = new Attribute { Name = "connectionId", Type = AttributeType.STRING },
            ProjectionType = ProjectionType.ALL
        });

        // --- QueuedMessages table ---
        // PK: USER#{driverId}, SK: MSG#{occurredAt}#{eventId} (chronological drain)
        // TTL: expiresAt (24h per Req 3.6)
        QueuedMessagesTable = new Table(this, "QueuedMessages", new TableProps
        {
            TableName = "BiyaHero-QueuedMessages",
            PartitionKey = new Attribute { Name = "pk", Type = AttributeType.STRING },
            SortKey = new Attribute { Name = "sk", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            TimeToLiveAttribute = "expiresAt",
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // ─────────────────────────────────────────────────────────────────────
        // KMS Keys — JWT signing and webhook signing
        // Free tier: 20k requests/month always-free
        // Keys are cached in-process (task 2.6) to stay within limits
        // ─────────────────────────────────────────────────────────────────────

        JwtSigningKey = new Key(this, "JwtSigningKey", new KeyProps
        {
            Alias = "alias/biyahero-jwt-signing",
            Description = "HS256 JWT signing secret for BiyaHero auth tokens",
            KeySpec = KeySpec.SYMMETRIC_DEFAULT,
            KeyUsage = KeyUsage.ENCRYPT_DECRYPT,
            EnableKeyRotation = true,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        WebhookSigningKey = new Key(this, "WebhookSigningKey", new KeyProps
        {
            Alias = "alias/biyahero-webhook-signing",
            Description = "HMAC-SHA256 webhook signature verification secret for wallet adapter",
            KeySpec = KeySpec.SYMMETRIC_DEFAULT,
            KeyUsage = KeyUsage.ENCRYPT_DECRYPT,
            EnableKeyRotation = true,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // ─────────────────────────────────────────────────────────────────────
        // Outputs — export ARNs and endpoints for cross-stack references
        // ─────────────────────────────────────────────────────────────────────

        _ = new CfnOutput(this, "DbEndpoint", new CfnOutputProps
        {
            Value = Database.DbInstanceEndpointAddress,
            Description = "RDS PostgreSQL endpoint address",
            ExportName = "BiyaHero-DbEndpoint"
        });

        _ = new CfnOutput(this, "DbSecretArn", new CfnOutputProps
        {
            Value = Database.Secret!.SecretArn,
            Description = "RDS credentials secret ARN",
            ExportName = "BiyaHero-DbSecretArn"
        });

        _ = new CfnOutput(this, "DemandPingsTableArn", new CfnOutputProps
        {
            Value = DemandPingsTable.TableArn,
            Description = "DemandPings DynamoDB table ARN",
            ExportName = "BiyaHero-DemandPingsTableArn"
        });

        _ = new CfnOutput(this, "PaymentEventsTableArn", new CfnOutputProps
        {
            Value = PaymentEventsTable.TableArn,
            Description = "PaymentEvents DynamoDB table ARN",
            ExportName = "BiyaHero-PaymentEventsTableArn"
        });

        _ = new CfnOutput(this, "WsConnectionsTableArn", new CfnOutputProps
        {
            Value = WsConnectionsTable.TableArn,
            Description = "WsConnections DynamoDB table ARN",
            ExportName = "BiyaHero-WsConnectionsTableArn"
        });

        _ = new CfnOutput(this, "QueuedMessagesTableArn", new CfnOutputProps
        {
            Value = QueuedMessagesTable.TableArn,
            Description = "QueuedMessages DynamoDB table ARN",
            ExportName = "BiyaHero-QueuedMessagesTableArn"
        });

        _ = new CfnOutput(this, "JwtSigningKeyArn", new CfnOutputProps
        {
            Value = JwtSigningKey.KeyArn,
            Description = "KMS key ARN for JWT signing",
            ExportName = "BiyaHero-JwtSigningKeyArn"
        });

        _ = new CfnOutput(this, "WebhookSigningKeyArn", new CfnOutputProps
        {
            Value = WebhookSigningKey.KeyArn,
            Description = "KMS key ARN for webhook signing",
            ExportName = "BiyaHero-WebhookSigningKeyArn"
        });
    }
}
