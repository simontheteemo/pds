using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Shared;
using PDS.Shared.Settings;

namespace PDS.Portfolio.Features;

internal sealed record ListProjectsQuery(
    ProjectStage? Stage, ProjectStatus? Status, string? Search, bool? IncludeArchived, int? Page, int? PageSize);

internal sealed record ProjectListItem(
    Guid Id,
    string Code,
    string Name,
    ProjectStage Stage,
    ProjectStatus Status,
    string? Suburb,
    string City,
    DateOnly? PlannedCompletion,
    decimal? BudgetAmount,
    string Currency,
    bool IsArchived);

internal static class ListProjects
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/projects", Handle).WithName("ListProjects");

    internal static async Task<Ok<PagedResult<ProjectListItem>>> Handle(
        [AsParameters] ListProjectsQuery query, ProjectStore store, IOptions<LocaleOptions> locale, CancellationToken ct)
    {
        var projects = await store.ListAllAsync(ct);
        return TypedResults.Ok(Apply(projects, query, locale.Value.Currency));
    }

    internal static PagedResult<ProjectListItem> Apply(IEnumerable<Project> projects, ListProjectsQuery query, string currency)
    {
        var page = Math.Max(1, query.Page ?? 1);
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);
        var term = query.Search?.Trim();

        var matches = projects
            .Where(p => query.IncludeArchived == true || !p.IsArchived)
            .Where(p => query.Stage is null || p.Fields.Stage == query.Stage)
            .Where(p => query.Status is null || p.Fields.Status == query.Status)
            .Where(p => string.IsNullOrEmpty(term) || Matches(p, term))
            .OrderBy(p => p.Fields.Code, StringComparer.Ordinal)
            .ToList();

        var skip = (long)(page - 1) * pageSize;
        IReadOnlyList<ProjectListItem> items = skip >= matches.Count
            ? []
            : matches.Skip((int)skip).Take(pageSize).Select(p => ToListItem(p, currency)).ToList();

        return new PagedResult<ProjectListItem>(items, page, pageSize, matches.Count);
    }

    private static bool Matches(Project project, string term) =>
        new[] { project.Fields.Code, project.Fields.Name, project.Fields.Site.Suburb, project.Fields.Site.City }
            .Any(value => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);

    private static ProjectListItem ToListItem(Project p, string currency) => new(
        p.Id.Value, p.Fields.Code, p.Fields.Name, p.Fields.Stage, p.Fields.Status, p.Fields.Site.Suburb, p.Fields.Site.City,
        p.Fields.PlannedCompletion, p.Fields.Budget?.Amount, p.Fields.Budget?.Currency ?? currency, p.IsArchived);
}
