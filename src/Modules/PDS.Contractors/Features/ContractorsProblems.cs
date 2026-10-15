using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Shared.Web;

namespace PDS.Contractors.Features;

internal static class ContractorsProblems
{
    public static ProblemHttpResult VersionConflict(string entity) => Problems.Conflict(
        $"This {entity} was changed by someone else.",
        $"Reload the {entity} to see the latest changes, then apply your edits again.");

    public static ValidationProblem ProjectArchived() =>
        Problems.Field("projectId", "Contracts can't be added to an archived project.");

    public static ValidationProblem ContractorUnavailable() =>
        Problems.Field("contractorId", "Choose an active contractor.");

    public static ValidationProblem ContractorChanged() =>
        Problems.Field("contractorId", "A contract's contractor can't be changed.");

    public static ValidationProblem InvalidType() =>
        Problems.Field("type", "Choose a valid contractor type.");
}
