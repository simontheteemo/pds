using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Data;
using PDS.Portfolio.Domain;
using PDS.Shared.Security;
using PDS.Shared.Settings;

namespace PDS.Portfolio.Features;

internal static class AddStandardMilestones
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/projects/{id:guid}/milestones/standard", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithName("AddStandardMilestones");

    /// <summary>Adds the configured standard milestones that don't exist yet (matched by name). Returns only the added ones.</summary>
    internal static async Task<Results<Ok<List<MilestoneDetails>>, NotFound, ValidationProblem>> Handle(
        Guid id, ProjectStore projects, MilestoneStore store, ICurrentUser user, TimeProvider clock,
        IOptions<LocaleOptions> locale, IOptions<PortfolioOptions> options, CancellationToken ct)
    {
        var pid = new ProjectId(id);
        var project = await projects.GetAsync(pid, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return MilestoneProblems.ProjectArchived();

        var current = await store.ListForProjectAsync(pid, ct);
        var existing = current.Select(m => MilestoneRules.NameKey(m.Fields.Name)).ToHashSet();
        var sortOrder = current.Count == 0 ? 10 : (current.Max(m => m.Fields.SortOrder) / 10 + 1) * 10;
        var stamp = new AuditStamp(clock.GetUtcNow(), user.Name);

        // Build everything first so bad configuration can never leave a partial write.
        var toAdd = new List<Milestone>();
        foreach (var configured in options.Value.EffectiveStandardMilestones)
        {
            var name = configured?.Name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > MilestoneRules.NameMaxLength)
                continue;
            if (!Enum.TryParse<MilestoneCategory>(configured!.Category, ignoreCase: true, out var category) || !Enum.IsDefined(category))
                continue;
            if (!existing.Add(MilestoneRules.NameKey(name)))
                continue;
            var order = Math.Min(sortOrder, MilestoneRules.MaxSortOrder);
            toAdd.Add(Milestone.Create(pid, new MilestoneFields(name, category, null, null, null, null, null, order), stamp));
            sortOrder += 10;
        }

        var today = LocalDate.Today(clock, locale.Value);
        var added = new List<MilestoneDetails>();
        foreach (var milestone in toAdd)
        {
            await store.CreateAsync(milestone, ct);
            added.Add(MilestoneDetails.From(milestone, today));
        }

        return TypedResults.Ok(added);
    }
}
