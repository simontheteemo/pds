using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Budgets;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AwsApigatewayv2Authorizers;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;
using ApiHttpMethod = Amazon.CDK.AWS.Apigatewayv2.HttpMethod;
using LambdaFunction = Amazon.CDK.AWS.Lambda.Function;
using LambdaFunctionProps = Amazon.CDK.AWS.Lambda.FunctionProps;
using S3AssetOptions = Amazon.CDK.AWS.S3.Assets.AssetOptions;
using CloudFrontDistribution = Amazon.CDK.AWS.CloudFront.Distribution;

namespace PDS.Infra;

/// <summary>SPA on S3 + CloudFront, API Lambda behind an HTTP API with a Cognito JWT authorizer, alarms and a budget.</summary>
public sealed class AppStack : Stack
{
    public AppStack(
        Construct scope,
        string id,
        IStackProps props,
        DeploymentSettings settings,
        AssetPaths assets,
        IReadOnlyDictionary<string, Table> tables,
        IUserPool userPool,
        UserPoolDomain domain)
        : base(scope, id, props)
    {
        // Static site
        var site = new Bucket(this, "site", new BucketProps
        {
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            EnforceSSL = true,
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,
        });
        var cdn = new CloudFrontDistribution(this, "cdn", new DistributionProps
        {
            DefaultBehavior = new BehaviorOptions
            {
                Origin = S3BucketOrigin.WithOriginAccessControl(site),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
            },
            DefaultRootObject = "index.html",
            ErrorResponses =
            [
                new ErrorResponse { HttpStatus = 403, ResponseHttpStatus = 200, ResponsePagePath = "/index.html", Ttl = Duration.Seconds(0) },
                new ErrorResponse { HttpStatus = 404, ResponseHttpStatus = 200, ResponsePagePath = "/index.html", Ttl = Duration.Seconds(0) },
            ],
        });
        var siteUrl = $"https://{cdn.DistributionDomainName}";

        // Sign-in client for the SPA (authorization code + PKCE, no secret)
        var client = new UserPoolClient(this, "spa-client", new UserPoolClientProps
        {
            UserPool = userPool,
            UserPoolClientName = $"{settings.Name}-spa",
            GenerateSecret = false,
            AuthFlows = new AuthFlow { UserSrp = true },
            OAuth = new OAuthSettings
            {
                Flows = new OAuthFlows { AuthorizationCodeGrant = true },
                Scopes = [OAuthScope.OPENID, OAuthScope.EMAIL, OAuthScope.PROFILE],
                CallbackUrls = [$"{siteUrl}/"],
                LogoutUrls = [$"{siteUrl}/"],
            },
            SupportedIdentityProviders = [UserPoolClientIdentityProvider.COGNITO],
            PreventUserExistenceErrors = true,
            IdTokenValidity = Duration.Hours(1),
            AccessTokenValidity = Duration.Hours(1),
            RefreshTokenValidity = Duration.Days(7),
        });
        var issuer = $"https://cognito-idp.{Region}.amazonaws.com/{userPool.UserPoolId}";

        // API function
        var environment = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Auth__Mode"] = "Gateway",
            ["Auth__Authority"] = issuer,
            ["Auth__ClientId"] = client.UserPoolClientId,
            ["Auth__LogoutDomain"] = domain.BaseUrl(),
            ["Branding__ProductName"] = settings.Branding.ProductName,
            ["Branding__PrimaryColor"] = settings.Branding.PrimaryColor,
            ["Locale__Currency"] = settings.Locale.Currency,
            ["Locale__Culture"] = settings.Locale.Culture,
            ["Locale__TimeZone"] = settings.Locale.TimeZone,
        };
        if (settings.Branding.LogoUrl is { } logoUrl)
            environment["Branding__LogoUrl"] = logoUrl;
        foreach (var (logicalName, table) in tables)
            environment[$"Tables__{logicalName}"] = table.TableName;

        var api = new LambdaFunction(this, "api", new LambdaFunctionProps
        {
            FunctionName = $"{settings.Name}-api",
            Runtime = Runtime.DOTNET_10,
            Architecture = Architecture.ARM_64,
            Handler = "PDS.Api",
            Code = Code.FromAsset(assets.Api),
            MemorySize = 1024,
            Timeout = Duration.Seconds(30),
            Environment = environment,
            LogGroup = new LogGroup(this, "api-logs", new LogGroupProps
            {
                LogGroupName = $"/aws/lambda/{settings.Name}-api",
                Retention = RetentionDays.THREE_MONTHS,
                RemovalPolicy = RemovalPolicy.DESTROY,
            }),
        });
        foreach (var table in tables.Values)
            table.GrantReadWriteData(api);

        // HTTP API: /api/config is public; everything else requires a Cognito ID token for this client
        var integration = new HttpLambdaIntegration("api-integration", api);
        var authorizer = new HttpJwtAuthorizer("cognito", issuer, new HttpJwtAuthorizerProps { JwtAudience = [client.UserPoolClientId] });
        var httpApi = new HttpApi(this, "http-api", new HttpApiProps
        {
            ApiName = $"{settings.Name}-api",
            CorsPreflight = new CorsPreflightOptions
            {
                AllowOrigins = [siteUrl],
                AllowMethods = [CorsHttpMethod.GET, CorsHttpMethod.POST, CorsHttpMethod.PUT, CorsHttpMethod.OPTIONS],
                AllowHeaders = ["authorization", "content-type", "x-correlation-id"],
                ExposeHeaders = ["location", "x-correlation-id"],
                MaxAge = Duration.Hours(1),
            },
        });
        httpApi.AddRoutes(new AddRoutesOptions { Path = "/api/config", Methods = [ApiHttpMethod.GET], Integration = integration });
        httpApi.AddRoutes(new AddRoutesOptions { Path = "/api/{proxy+}", Methods = [ApiHttpMethod.ANY], Integration = integration, Authorizer = authorizer });

        // SPA upload; config.json is generated per deployment so one build serves every client
        _ = new BucketDeployment(this, "site-deployment", new BucketDeploymentProps
        {
            DestinationBucket = site,
            Sources =
            [
                Source.Asset(assets.Web, new S3AssetOptions { Exclude = ["config.json"] }),
                Source.JsonData("config.json", new Dictionary<string, object> { ["apiBaseUrl"] = httpApi.ApiEndpoint }),
            ],
            Distribution = cdn,
            DistributionPaths = ["/*"],
        });

        // Alarms and budget
        var alarms = new Topic(this, "alarms", new TopicProps { TopicName = $"{settings.Name}-alarms" });
        if (settings.AlarmEmail is { } email)
            alarms.AddSubscription(new EmailSubscription(email));
        var notify = new SnsAction(alarms);
        var fiveMinutes = new MetricOptions { Period = Duration.Minutes(5), Statistic = "Sum" };

        void AddAlarm(string alarmId, IMetric metric, string description) =>
            new Alarm(this, alarmId, new AlarmProps
            {
                Metric = metric,
                Threshold = 1,
                EvaluationPeriods = 1,
                ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
                TreatMissingData = TreatMissingData.NOT_BREACHING,
                AlarmDescription = description,
            }).AddAlarmAction(notify);

        AddAlarm("api-5xx", httpApi.MetricServerError(fiveMinutes), "The API returned 5xx responses.");
        AddAlarm("api-function-errors", api.MetricErrors(fiveMinutes), "The API function failed.");
        AddAlarm("api-function-throttles", api.MetricThrottles(fiveMinutes), "The API function was throttled.");

        _ = new CfnBudget(this, "monthly-budget", new CfnBudgetProps
        {
            Budget = new CfnBudget.BudgetDataProperty
            {
                BudgetName = $"{settings.Name}-monthly",
                BudgetType = "COST",
                TimeUnit = "MONTHLY",
                BudgetLimit = new CfnBudget.SpendProperty { Amount = (double)settings.MonthlyBudgetUsd, Unit = "USD" },
            },
            NotificationsWithSubscribers = settings.AlarmEmail is { } budgetEmail
                ? new object[]
                {
                    new CfnBudget.NotificationWithSubscribersProperty
                    {
                        Notification = new CfnBudget.NotificationProperty
                        {
                            NotificationType = "ACTUAL",
                            ComparisonOperator = "GREATER_THAN",
                            Threshold = 80,
                            ThresholdType = "PERCENTAGE",
                        },
                        Subscribers = new object[] { new CfnBudget.SubscriberProperty { SubscriptionType = "EMAIL", Address = budgetEmail } },
                    },
                }
                : null,
        });

        _ = new CfnOutput(this, "SiteUrl", new CfnOutputProps { Value = siteUrl });
        _ = new CfnOutput(this, "ApiUrl", new CfnOutputProps { Value = httpApi.ApiEndpoint });
        _ = new CfnOutput(this, "UserPoolId", new CfnOutputProps { Value = userPool.UserPoolId });
    }
}
