using PDS.Costs.Domain;

namespace PDS.Costs.Data;

/// <summary>Everything stored for one project's cost report.</summary>
internal sealed record ProjectCostData(IReadOnlyList<BudgetLine> Lines);
