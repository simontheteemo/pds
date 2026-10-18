using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using PDS.Portfolio.Contracts;
using PDS.Risks.Data;
using PDS.Risks.Domain;

namespace PDS.Risks.Features;

internal sealed record HighRiskItem(
    Guid Id, Guid ProjectId, string ProjectCode, string ProjectName, string Title, RiskCategory Category, int Score,
    RiskBand Band, RiskStatus Status, string? Owner, DateOnly? DueDate);

internal static class ListHighRisks
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/high", Handle).WithName("ListHighRisks");

    /// <summary>Open risks banded High or Extreme across active projects, highest score first.</summary>
    internal static async Task<Ok<List<HighRiskItem>>> Handle(
        RiskStore store, IPortfolioQueries portfolio, CancellationToken ct)
    {
        var risks = (await store.ScanAllAsync(ct))
            .Where(r => r.Status != RiskStatus.Closed && r.Band is RiskBand.High or RiskBand.Extreme)
            .ToList();
        if (risks.Count == 0)
            return TypedResults.Ok(new List<HighRiskItem>());

        var ids = risks.Select(r => r.ProjectId).Distinct().ToList();
        var projects = (await portfolio.GetSummaries(ids, ct)).Where(p => !p.IsArchived).ToDictionary(p => p.Id);
        var items = risks
            .Where(r => projects.ContainsKey(r.ProjectId))
            .Select(r => (Risk: r, Project: projects[r.ProjectId]))
            .OrderByDescending(x => x.Risk.Score)
            .ThenBy(x => x.Project.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Risk.Fields.Title, StringComparer.OrdinalIgnoreCase)
            .Select(x => new HighRiskItem(
                x.Risk.Id.Value, x.Risk.ProjectId.Value, x.Project.Code, x.Project.Name, x.Risk.Fields.Title,
                x.Risk.Fields.Category, x.Risk.Score, x.Risk.Band, x.Risk.Status, x.Risk.Fields.Owner,
                x.Risk.Fields.DueDate))
            .ToList();
        return TypedResults.Ok(items);
    }
}
