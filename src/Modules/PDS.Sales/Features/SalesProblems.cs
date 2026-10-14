using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Shared.Web;

namespace PDS.Sales.Features;

internal static class SalesProblems
{
    public static ValidationProblem ProjectArchived() =>
        Problems.Field("projectId", "Units can't be added to an archived project.");

    public static ValidationProblem IdentifierTaken() =>
        Problems.Field("identifier", "Another unit in this project already uses this identifier.");

    public static ProblemHttpResult VersionConflict() => Problems.Conflict(
        "This unit was changed by someone else.",
        "Reload the units to see the latest changes, then apply your edits again.");
}
