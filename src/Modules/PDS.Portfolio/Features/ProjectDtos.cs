using PDS.Portfolio.Domain;
using PDS.Shared;

namespace PDS.Portfolio.Features;

internal sealed record SiteDto(
    string AddressLine,
    string? Suburb,
    string City,
    string? Region,
    string? Postcode,
    string? LegalDescription,
    string? TitleReference,
    decimal? LandAreaSqm)
{
    public Site ToSite() => new(AddressLine, Suburb, City, Region, Postcode, LegalDescription, TitleReference, LandAreaSqm);

    public static SiteDto From(Site s) =>
        new(s.AddressLine, s.Suburb, s.City, s.Region, s.Postcode, s.LegalDescription, s.TitleReference, s.LandAreaSqm);
}

/// <summary>Fields shared by create and update requests. Stage and status are nullable so omission is a 400.</summary>
internal interface IProjectInput
{
    string Code { get; }

    string Name { get; }

    SiteDto Site { get; }

    ProjectStage? Stage { get; }

    ProjectStatus? Status { get; }

    DateOnly? PlannedStart { get; }

    DateOnly? PlannedCompletion { get; }

    DateOnly? ActualStart { get; }

    DateOnly? ActualCompletion { get; }

    decimal? BudgetAmount { get; }

    string? ProjectManager { get; }

    string? Description { get; }
}

internal sealed record CreateProjectRequest(
    string Code,
    string Name,
    SiteDto Site,
    ProjectStage? Stage,
    ProjectStatus? Status,
    DateOnly? PlannedStart,
    DateOnly? PlannedCompletion,
    DateOnly? ActualStart,
    DateOnly? ActualCompletion,
    decimal? BudgetAmount,
    string? ProjectManager,
    string? Description) : IProjectInput;

internal sealed record UpdateProjectRequest(
    string Code,
    string Name,
    SiteDto Site,
    ProjectStage? Stage,
    ProjectStatus? Status,
    DateOnly? PlannedStart,
    DateOnly? PlannedCompletion,
    DateOnly? ActualStart,
    DateOnly? ActualCompletion,
    decimal? BudgetAmount,
    string? ProjectManager,
    string? Description,
    long Version) : IProjectInput;

internal static class ProjectInputMapping
{
    /// <summary>Call only after validation: stage and status are known to be present.</summary>
    public static ProjectFields ToFields(this IProjectInput input, string currency) => new(
        input.Code,
        input.Name,
        input.Site.ToSite(),
        input.Stage!.Value,
        input.Status!.Value,
        input.PlannedStart,
        input.PlannedCompletion,
        input.ActualStart,
        input.ActualCompletion,
        input.BudgetAmount is { } amount ? new Money(amount, currency) : null,
        input.ProjectManager,
        input.Description);
}

internal sealed record ProjectDetails(
    Guid Id,
    string Code,
    string Name,
    SiteDto Site,
    ProjectStage Stage,
    ProjectStatus Status,
    DateOnly? PlannedStart,
    DateOnly? PlannedCompletion,
    DateOnly? ActualStart,
    DateOnly? ActualCompletion,
    decimal? BudgetAmount,
    string Currency,
    string? ProjectManager,
    string? Description,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version)
{
    public static ProjectDetails From(Project project, string defaultCurrency)
    {
        var f = project.Fields;
        return new ProjectDetails(
            project.Id.Value, f.Code, f.Name, SiteDto.From(f.Site), f.Stage, f.Status,
            f.PlannedStart, f.PlannedCompletion, f.ActualStart, f.ActualCompletion,
            f.Budget?.Amount, f.Budget?.Currency ?? defaultCurrency, f.ProjectManager, f.Description,
            project.IsArchived, project.Created.At, project.Created.By, project.Updated.At, project.Updated.By,
            project.Version);
    }
}
