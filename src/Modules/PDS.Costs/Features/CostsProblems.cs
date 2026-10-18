using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Shared.Web;

namespace PDS.Costs.Features;

internal static class CostsProblems
{
    public static ValidationProblem ProjectArchived() =>
        Problems.Field("projectId", "Costs can't be changed on an archived project.");

    public static ValidationProblem DuplicateLineName() =>
        Problems.Field("name", "A budget line with this name already exists.");

    public static ValidationProblem LineInUse() =>
        Problems.Field("budgetLineId", "This line has commitments or actuals.");

    public static ValidationProblem UnknownLine() =>
        Problems.Field("budgetLineId", "Choose a budget line in this project.");

    public static ValidationProblem UnknownContract() =>
        Problems.Field("contractId", "Choose a contract on this project.");

    public static ValidationProblem ContractorUnknown() =>
        Problems.Field("contractorId", "Choose a contractor.");

    public static ValidationProblem ContractorMismatch() =>
        Problems.Field("contractorId", "The contractor must match the contract's contractor.");

    public static ValidationProblem ContractorInactive() =>
        Problems.Field("contractorId", "Choose an active contractor.");

    public static ValidationProblem DuplicateInvoiceNumber() =>
        Problems.Field("reference", "This contractor already has an invoice with this number.");

    public static ValidationProblem InvalidVersion() =>
        Problems.Field("version", "Version is required.");

    public static ProblemHttpResult VersionConflict(string entity) => Problems.Conflict(
        $"This {entity} was changed by someone else.",
        "Reload the cost report to see the latest changes, then apply your edits again.");
}
