using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Constructs;

namespace BiyaHero.Infra;

/// <summary>
/// CloudFront distribution + S3 origin for the PWA static build.
/// Enforces HTTP→HTTPS redirect and TLS 1.2+ minimum protocol.
/// Requirements: 6.1 (PWA hosting), 6.6 (Lighthouse PWA installability), 8.4 (TLS 1.2+).
/// </summary>
public class FrontendStack : Stack
{
    /// <summary>The S3 bucket holding the PWA static assets.</summary>
    public IBucket SiteBucket { get; }

    /// <summary>The CloudFront distribution serving the PWA.</summary>
    public IDistribution Distribution { get; }

    internal FrontendStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // S3 bucket for the PWA build output (private — no public access).
        SiteBucket = new Bucket(this, "PwaSiteBucket", new BucketProps
        {
            BucketName = $"biyahero-pwa-{Account}-{Region}",
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,
            // Serve index.html for SPA client-side routing fallback
            WebsiteIndexDocument = "index.html",
            WebsiteErrorDocument = "index.html"
        });

        // Origin Access Identity so CloudFront can read from the private bucket.
        var originAccessIdentity = new OriginAccessIdentity(this, "PwaOai", new OriginAccessIdentityProps
        {
            Comment = "OAI for BiyaHero PWA S3 bucket"
        });
        SiteBucket.GrantRead(originAccessIdentity);

        // CloudFront distribution with:
        // - HTTP→HTTPS redirect (ViewerProtocolPolicy.REDIRECT_TO_HTTPS)
        // - TLS 1.2+ minimum (SecurityPolicyProtocol.TLS_V1_2_2021)
        // - SPA fallback via custom error responses (404 → /index.html)
        Distribution = new Distribution(this, "PwaDistribution", new DistributionProps
        {
            Comment = "BiyaHero PWA CloudFront Distribution",
            DefaultBehavior = new BehaviorOptions
            {
                Origin = new S3Origin(SiteBucket, new S3OriginProps
                {
                    OriginAccessIdentity = originAccessIdentity
                }),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                AllowedMethods = AllowedMethods.ALLOW_GET_HEAD_OPTIONS,
                CachePolicy = CachePolicy.CACHING_OPTIMIZED,
                Compress = true
            },
            DefaultRootObject = "index.html",
            MinimumProtocolVersion = SecurityPolicyProtocol.TLS_V1_2_2021,
            HttpVersion = HttpVersion.HTTP2_AND_3,
            // SPA client-side routing: return index.html for 403/404 from S3
            ErrorResponses = new IErrorResponse[]
            {
                new ErrorResponse
                {
                    HttpStatus = 403,
                    ResponseHttpStatus = 200,
                    ResponsePagePath = "/index.html",
                    Ttl = Duration.Seconds(0)
                },
                new ErrorResponse
                {
                    HttpStatus = 404,
                    ResponseHttpStatus = 200,
                    ResponsePagePath = "/index.html",
                    Ttl = Duration.Seconds(0)
                }
            }
        });

        // Deploy the PWA build output to S3 and invalidate CloudFront cache.
        // The source path points to the Next.js export output directory.
        _ = new BucketDeployment(this, "DeployPwa", new BucketDeploymentProps
        {
            Sources = new[] { Source.Asset("../apps/web/out") },
            DestinationBucket = SiteBucket,
            Distribution = (Distribution)Distribution,
            DistributionPaths = new[] { "/*" }
        });

        // Stack outputs
        _ = new CfnOutput(this, "DistributionDomainName", new CfnOutputProps
        {
            Value = Distribution.DistributionDomainName,
            Description = "CloudFront distribution domain name for the BiyaHero PWA"
        });

        _ = new CfnOutput(this, "DistributionId", new CfnOutputProps
        {
            Value = Distribution.DistributionId,
            Description = "CloudFront distribution ID"
        });

        _ = new CfnOutput(this, "SiteBucketName", new CfnOutputProps
        {
            Value = SiteBucket.BucketName,
            Description = "S3 bucket name holding the PWA static assets"
        });
    }
}
