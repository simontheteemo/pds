using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Shared.Web;

namespace PDS.Risks.Features;

internal static class RisksProblems
{
    public static ValidationProblem ProjectArchived() =>
        Problems.Field("projectId", "Risks can't be added to an archived project.");

    public static ValidationProblem RiskClosed() =>
        Problems.Field("status", "Reopen the risk before editing it.");

    public static ProblemHttpResult VersionConflict() => Problems.Conflict(
        "This risk was changed by someone else.",
        "Reload the risks to see the latest changes, then apply your edits again.");
}
