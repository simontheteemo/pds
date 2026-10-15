using Microsoft.Extensions.Options;
using PDS.Portfolio.Contracts;
using PDS.Reporting.Contracts;
using PDS.Sales.Data;
using PDS.Sales.Domain;
using PDS.Shared.Settings;

namespace PDS.Sales.Features;

internal sealed class UnitsExport(UnitStore store, IPortfolioQueries portfolio, IOptions<LocaleOptions> locale) : IExportDataset
{
    public string Name => "units";

    public string Title => "Units and sales";

    public string Description => "Every unit or lot with its listing details and current sale: status, buyer, price and key dates.";

    public async Task<ExportTable> LoadAsync(CancellationToken ct)
    {
        var currency = locale.Value.Currency;
        var units = await store.ListAllAsync(ct);
        var projects = (await portfolio.GetSummaries(units.Select(u => u.ProjectId).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);
        var rows = units
            .Select(u => (Unit: u, Project: projects.GetValueOrDefault(u.ProjectId)))
            .OrderBy(x => x.Project?.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Unit.Fields.Identifier, UnitIdentifiers.Order);

        return ExportTable.From<(Unit Unit, ProjectSummary? Project)>(rows,
        [
            new("unit_id", x => x.Unit.Id.Value),
            new("project_id", x => x.Unit.ProjectId.Value),
            new("project_code", x => x.Project?.Code),
            new("project_name", x => x.Project?.Name),
            new("identifier", x => x.Unit.Fields.Identifier),
            new("type", x => x.Unit.Fields.Type),
            new("bedrooms", x => x.Unit.Fields.Bedrooms),
            new("bathrooms", x => x.Unit.Fields.Bathrooms),
            new("car_parks", x => x.Unit.Fields.CarParks),
            new("floor_area_sqm", x => x.Unit.Fields.FloorAreaSqm),
            new("land_area_sqm", x => x.Unit.Fields.LandAreaSqm),
            new("list_price", x => x.Unit.Fields.ListPrice?.Amount),
            new("sale_status", x => x.Unit.Status),
            new("buyer_name", x => x.Unit.Sale?.BuyerName),
            new("buyer_solicitor", x => x.Unit.Sale?.BuyerSolicitor),
            new("agreement_date", x => x.Unit.Sale?.AgreementDate),
            new("sale_price", x => x.Unit.Sale?.SalePrice?.Amount),
            new("deposit", x => x.Unit.Sale?.Deposit?.Amount),
            new("unconditional_date", x => x.Unit.Sale?.UnconditionalDate),
            new("settlement_date", x => x.Unit.Sale?.SettlementDate),
            new("currency", x => x.Unit.Currency ?? currency),
            new("notes", x => x.Unit.Fields.Notes),
            new("created_at", x => x.Unit.Created.At),
            new("updated_at", x => x.Unit.Updated.At),
        ]);
    }
}
