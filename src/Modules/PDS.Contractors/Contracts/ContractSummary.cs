using PDS.Portfolio.Contracts;

namespace PDS.Contractors.Contracts;

public sealed record ContractSummary(
    ContractId Id, ProjectId ProjectId, ContractorId ContractorId, string ContractorName,
    string Title, string? Reference, string Status, decimal? ValueAmount, string? Currency);
