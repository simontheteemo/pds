using Amazon.CDK;
using Amazon.CDK.AWS.Cognito;
using Constructs;
using PDS.Shared.Security;

namespace PDS.Infra;

/// <summary>Cognito user pool with one group per PDS role. Users are created by an administrator.</summary>
public sealed class AuthStack : Stack
{
    public AuthStack(Construct scope, string id, IStackProps props, DeploymentSettings settings) : base(scope, id, props)
    {
        UserPool = new UserPool(this, "users", new UserPoolProps
        {
            UserPoolName = $"{settings.Name}-users",
            SelfSignUpEnabled = false,
            SignInAliases = new SignInAliases { Email = true },
            AutoVerify = new AutoVerifiedAttrs { Email = true },
            StandardAttributes = new StandardAttributes
            {
                Email = new StandardAttribute { Required = true, Mutable = true },
                Fullname = new StandardAttribute { Required = false, Mutable = true },
            },
            PasswordPolicy = new PasswordPolicy
            {
                MinLength = 12,
                RequireLowercase = true,
                RequireUppercase = true,
                RequireDigits = true,
                RequireSymbols = false,
            },
            AccountRecovery = AccountRecovery.EMAIL_ONLY,
            DeletionProtection = settings.IsProduction,
            RemovalPolicy = settings.IsProduction ? RemovalPolicy.RETAIN : RemovalPolicy.DESTROY,
        });

        foreach (var role in Roles.All)
        {
            _ = new CfnUserPoolGroup(this, $"group-{role.ToLowerInvariant()}", new CfnUserPoolGroupProps
            {
                UserPoolId = UserPool.UserPoolId,
                GroupName = role,
                Description = $"PDS {role}",
            });
        }

        Domain = UserPool.AddDomain("domain", new UserPoolDomainOptions
        {
            CognitoDomain = new CognitoDomainOptions { DomainPrefix = settings.CognitoDomainPrefix },
        });
    }

    public UserPool UserPool { get; }

    public UserPoolDomain Domain { get; }
}
