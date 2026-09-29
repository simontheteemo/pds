using Amazon.CDK;
using PDS.Shared.Data;

namespace PDS.Infra;

public static class PdsApp
{
    /// <summary>
    /// Stacks for one deployment. The GitHub OIDC stack is defined when settings include GitHub, but CI deploys only
    /// the data, auth and app stacks; the OIDC stack is deployed by hand once per account.
    /// </summary>
    public static (DataStack Data, AuthStack Auth, AppStack App) Define(
        App app, DeploymentSettings settings, AssetPaths assets, IReadOnlyList<TableDefinition> tables)
    {
        var props = new StackProps { Env = new Amazon.CDK.Environment { Account = settings.Account, Region = settings.Region } };
        var data = new DataStack(app, $"{settings.Name}-data", props, settings, tables);
        var auth = new AuthStack(app, $"{settings.Name}-auth", props, settings);
        var web = new AppStack(app, $"{settings.Name}-app", props, settings, assets, data.Tables, auth.UserPool, auth.Domain);
        if (settings.GitHub is { } github)
            _ = new GitHubOidcStack(app, $"{settings.Name}-github-oidc", props, github);

        Tags.Of(app).Add("pds:deployment", settings.Name);
        return (data, auth, web);
    }
}
