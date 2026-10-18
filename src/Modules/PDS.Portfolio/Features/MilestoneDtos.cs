using PDS.Portfolio.Domain;

namespace PDS.Portfolio.Features;

/// <summary>Editable fields. Optional values are nullable so omission is a 400 where required, not a default.</summary>
internal interface IMilestoneInput
{
    string? Name { get; }

    MilestoneCategory? Category { get; }

    DateOnly? PlannedDate { get; }

    DateOnly? ForecastDate { get; }

    DateOnly? ActualDate { get; }

    string? Owner { get; }

    string? Notes { get; }

    int? SortOrder { get; }
}

internal sealed record CreateMilestoneRequest(
    string? Name, MilestoneCategory? Category, DateOnly? PlannedDate, DateOnly? ForecastDate, DateOnly? ActualDate,
    string? Owner, string? Notes, int? SortOrder) : IMilestoneInput;

internal sealed record UpdateMilestoneRequest(
    string? Name, MilestoneCategory? Category, DateOnly? PlannedDate, DateOnly? ForecastDate, DateOnly? ActualDate,
    string? Owner, string? Notes, int? SortOrder, long Version) : IMilestoneInput;

internal static class MilestoneInputMapping
{
    /// <summary>Call only after validation. <paramref name="defaultSortOrder"/> applies when the request omits one.</summary>
    public static MilestoneFields ToFields(this IMilestoneInput input, int defaultSortOrder) => new(
        input.Name!, input.Category!.Value, input.PlannedDate, input.ForecastDate, input.ActualDate,
        input.Owner, input.Notes, input.SortOrder ?? defaultSortOrder);
}

internal sealed record MilestoneDetails(
    Guid Id,
    Guid ProjectId,
    string Name,
    MilestoneCategory Category,
    DateOnly? PlannedDate,
    DateOnly? ForecastDate,
    DateOnly? ActualDate,
    MilestoneStatus Status,
    int? SlippageDays,
    int? DaysOverdue,
    string? Owner,
    string? Notes,
    int SortOrder,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static MilestoneDetails From(Milestone m, DateOnly today)
    {
        var f = m.Fields;
        var e = MilestoneRules.Evaluate(f, today);
        return new MilestoneDetails(
            m.Id.Value, m.ProjectId.Value, f.Name, f.Category, f.PlannedDate, f.ForecastDate, f.ActualDate,
            e.Status, e.SlippageDays, e.DaysOverdue, f.Owner, f.Notes, f.SortOrder,
            m.Created.At, m.Created.By, m.Updated.At, m.Updated.By, m.Version);
    }
}

internal sealed record MilestoneList(DateOnly Today, List<MilestoneDetails> Items);
