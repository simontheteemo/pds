using Microsoft.Extensions.Options;
using PDS.Consents.Data;
using PDS.Consents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Settings;

namespace PDS.Consents.Features;

internal sealed record DueItemsResult(DateOnly Today, List<DueItem> Items);

/// <summary>
/// The cross-project due list: overdue/due-soon conditions and expiries of active consents on non-archived
/// projects. Shared by GET /api/consents/due and the reminder digest so the rules live in one place.
/// </summary>
internal sealed class DueItemsQuery(
    ConsentStore store, IPortfolioQueries portfolio, TimeProvider clock, IOptions<LocaleOptions> locale)
{
    public async Task<DueItemsResult> LoadAsync(CancellationToken ct)
    {
        var today = LocalDate.Today(clock, locale.Value);
        var active = (await store.ScanAllAsync(ct)).Where(r => r.Consent.IsActive).ToList();
        var ids = active.Select(r => r.Consent.ProjectId).Distinct().ToList();
        var projects = (await portfolio.GetSummaries(ids, ct))
            .Where(p => !p.IsArchived)
            .ToDictionary(p => p.Id);

        var items = new List<DueItem>();
        foreach (var record in active)
        {
            var consent = record.Consent;
            if (!projects.TryGetValue(consent.ProjectId, out var project))
                continue;
            var f = consent.Fields;

            DueItem Make(DueItemKind kind, Condition? condition, string description, DateOnly date, DueState state) => new(
                kind, project.Id.Value, project.Code, project.Name, consent.Id.Value, f.Type, f.Title, f.Reference,
                condition?.Id.Value, condition?.Fields.Reference, description, date, state);

            var expiry = ConsentRules.ExpiryState(consent, today);
            if (expiry is DueState.Overdue or DueState.DueSoon)
                items.Add(Make(DueItemKind.Expiry, null, "Consent expires", f.ExpiryDate!.Value, expiry));

            foreach (var condition in record.Conditions)
            {
                var state = ConsentRules.ConditionState(condition, true, today);
                if (state is DueState.Overdue or DueState.DueSoon)
                    items.Add(Make(DueItemKind.Condition, condition, condition.Fields.Description, condition.Fields.DueDate!.Value, state));
            }
        }

        return new DueItemsResult(today, items
            .OrderBy(i => i.Date)
            .ThenBy(i => i.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.ConsentTitle, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }
}
