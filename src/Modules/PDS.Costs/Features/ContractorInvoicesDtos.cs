namespace PDS.Costs.Features;

internal sealed record ContractorInvoiceTotals(decimal Invoiced, decimal Paid, decimal Outstanding, decimal Disputed, decimal Gst);

internal sealed record ContractorInvoice(
    ActualCostDetails Entry, Guid ProjectId, string? ProjectCode, string? ProjectName, bool ProjectArchived,
    string BudgetLineName, string? ContractTitle);

internal sealed record ContractorInvoiceContract(
    Guid ContractId, Guid ProjectId, string? ProjectCode, string? ProjectName, string Title, string Status,
    decimal? ValueAmount, decimal InvoicedToDate, decimal? Remaining);

internal sealed record ContractorInvoices(
    Guid ContractorId, string ContractorName, bool ContractorActive, string Currency, ContractorInvoiceTotals Totals,
    List<ContractorInvoice> Invoices, List<ContractorInvoiceContract> Contracts);
