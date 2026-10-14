using PDS.Costs.Domain;
using PDS.Shared;

namespace PDS.Costs.Features;

/// <summary>Editable fields. Numbers are nullable so omission is a 400, not a default.</summary>
internal interface IBudgetLineInput
{
    string? Name { get; }

    int? SortOrder { get; }

    decimal? BudgetAmount { get; }

    decimal? ForecastRemainingAmount { get; }

    string? Notes { get; }
}

internal sealed record CreateBudgetLineRequest(
    string? Name, int? SortOrder, decimal? BudgetAmount, decimal? ForecastRemainingAmount, string? Notes) : IBudgetLineInput;

internal sealed record UpdateBudgetLineRequest(
    string? Name, int? SortOrder, decimal? BudgetAmount, decimal? ForecastRemainingAmount, string? Notes, long Version)
    : IBudgetLineInput;

internal static class BudgetLineInputMapping
{
    /// <summary>Call only after validation.</summary>
    public static BudgetLineFields ToFields(this IBudgetLineInput input, string currency) => new(
        input.Name!,
        input.SortOrder!.Value,
        new Money(input.BudgetAmount!.Value, currency),
        input.ForecastRemainingAmount is { } forecast ? new Money(forecast, currency) : null,
        input.Notes);
}

internal sealed record BudgetLineDetails(
    Guid Id,
    string Name,
    int SortOrder,
    decimal BudgetAmount,
    decimal? ForecastRemainingAmount,
    string Currency,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static BudgetLineDetails From(BudgetLine l)
    {
        var f = l.Fields;
        return new BudgetLineDetails(
            l.Id.Value, f.Name, f.SortOrder, f.Budget.Amount, f.ForecastRemaining?.Amount, f.Budget.Currency, f.Notes,
            l.Created.At, l.Created.By, l.Updated.At, l.Updated.By, l.Version);
    }
}
