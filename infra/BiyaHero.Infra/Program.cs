using Amazon.CDK;

namespace BiyaHero.Infra;

public class Program
{
    public static void Main(string[] args)
    {
        var app = new App();

        var env = new Amazon.CDK.Environment
        {
            Region = "us-east-1"
        };

        var dataStack = new DataStack(app, "BiyaHero-Data", new StackProps { Env = env });

        var apiStack = new ApiStack(app, "BiyaHero-Api", new ApiStackProps
        {
            Env = env,
            RdsInstance = dataStack.Database,
            Vpc = dataStack.Vpc,
            DatabaseSecurityGroup = dataStack.DatabaseSecurityGroup,
            DemandPingsTable = dataStack.DemandPingsTable,
            PaymentEventsTable = dataStack.PaymentEventsTable,
            WsConnectionsTable = dataStack.WsConnectionsTable,
            QueuedMessagesTable = dataStack.QueuedMessagesTable,
            JwtSigningKey = dataStack.JwtSigningKey,
            WebhookSigningKey = dataStack.WebhookSigningKey
        });
        apiStack.AddDependency(dataStack);

        var frontendStack = new FrontendStack(app, "BiyaHero-Frontend", new StackProps { Env = env });
        var monitoringStack = new MonitoringStack(app, "BiyaHero-Monitoring", new StackProps { Env = env });

        app.Synth();
    }
}
