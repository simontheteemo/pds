using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace PDS.Infra;

/// <summary>
/// Lets one GitHub repository environment deploy to this account without stored keys. Deploy it once per account
/// with administrator credentials; the role only assumes the CDK bootstrap roles.
/// </summary>
public sealed class GitHubOidcStack : Stack
{
    public GitHubOidcStack(Construct scope, string id, IStackProps props, GitHubSettings github) : base(scope, id, props)
    {
        var provider = new OpenIdConnectProvider(this, "github", new OpenIdConnectProviderProps
        {
            Url = "https://token.actions.githubusercontent.com",
            ClientIds = ["sts.amazonaws.com"],
        });

        var role = new Role(this, "deploy-role", new RoleProps
        {
            RoleName = "pds-github-deploy",
            Description = "Assumed by GitHub Actions to run cdk deploy through the CDK bootstrap roles.",
            MaxSessionDuration = Duration.Hours(1),
            AssumedBy = new WebIdentityPrincipal(provider.OpenIdConnectProviderArn, new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, object>
                {
                    ["token.actions.githubusercontent.com:aud"] = "sts.amazonaws.com",
                    ["token.actions.githubusercontent.com:sub"] = $"repo:{github.Repository}:environment:{github.Environment}",
                },
            }),
        });
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = ["sts:AssumeRole"],
            Resources = [$"arn:aws:iam::{Account}:role/cdk-*"],
        }));

        _ = new CfnOutput(this, "DeployRoleArn", new CfnOutputProps { Value = role.RoleArn });
    }
}
