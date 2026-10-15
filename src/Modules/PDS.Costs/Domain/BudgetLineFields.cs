using PDS.Shared;

namespace PDS.Costs.Domain;

internal sealed record BudgetLineFields(string Name, int SortOrder, Money Budget, Money? ForecastRemaining, string? Notes);
