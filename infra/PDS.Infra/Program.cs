using Amazon.CDK;
using PDS.Infra;
using PDS.Portfolio;

var app = new App();
var deployment = app.Node.TryGetContext("deployment") as string
    ?? throw new InvalidOperationException("Pass -c deployment=<name>; settings are read from deployments/<name>.json.");
var settings = DeploymentSettings.Load(Path.Combine("deployments", $"{deployment}.json"));
var assets = new AssetPaths(Api: Path.GetFullPath("../artifacts/api"), Web: Path.GetFullPath("../web/dist"));

PdsApp.Define(app, settings, assets, PortfolioModule.Tables);
app.Synth();
