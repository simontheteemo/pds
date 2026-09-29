using PDS.Shared.Data;

namespace PDS.Portfolio.Data;

internal static class PortfolioTable
{
    public const string LogicalName = "portfolio";
    public const string Gsi1 = "gsi1";

    public static readonly TableDefinition Definition = new(LogicalName, [new GlobalIndex(Gsi1, "gsi1pk", "gsi1sk")]);
}
