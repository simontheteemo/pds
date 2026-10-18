using Amazon.CDK;
using PDS.Actions;
using PDS.Consents;
using PDS.Contractors;
using PDS.Costs;
using PDS.Documents;
using PDS.Infra;
using PDS.Portfolio;
using PDS.Risks;
using PDS.Sales;

var app = new App();
var deployment = app.Node.TryGetContext("deployment") as string
    ?? throw new InvalidOperationException("Pass -c deployment=<name>; settings are read from deployments/<name>.json.");
var settings = DeploymentSettings.Load(Path.Combine("deployments", $"{deployment}.json"));
var assets = new AssetPaths(Api: Path.GetFullPath("../artifacts/api"), Web: Path.GetFullPath("../web/dist"));

PdsApp.Define(app, settings, assets, [.. PortfolioModule.Tables, .. RisksModule.Tables, .. ContractorsModule.Tables, .. ConsentsModule.Tables, .. SalesModule.Tables, .. DocumentsModule.Tables, .. CostsModule.Tables, .. ActionsModule.Tables]);
app.Synth();
