using Amazon.CDK;
using PDS.Shared.Data;

namespace PDS.Infra;

public static class PdsApp
{
    public static (DataStack Data, AuthStack Auth) Define(
        App app, DeploymentSettings settings, AssetPaths assets, IReadOnlyList<TableDefinition> tables)
    {
        var props = new StackProps { Env = new Amazon.CDK.Environment { Account = settings.Account, Region = settings.Region } };
        var data = new DataStack(app, $"{settings.Name}-data", props, settings, tables);
        var auth = new AuthStack(app, $"{settings.Name}-auth", props, settings);
        Tags.Of(app).Add("pds:deployment", settings.Name);
        _ = assets; // used by the app stack (Task 16)
        return (data, auth);
    }
}
