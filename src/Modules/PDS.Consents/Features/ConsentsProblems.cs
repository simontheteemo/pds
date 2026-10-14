using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Shared.Web;

namespace PDS.Consents.Features;

internal static class ConsentsProblems
{
    public static ValidationProblem ProjectArchived() =>
        Problems.Field("projectId", "Consents and conditions can't be added to an archived project.");

    public static ValidationProblem CompletedInFuture() =>
        Problems.Field("completedOn", "The completion date can't be in the future.");

    public static ProblemHttpResult VersionConflict(string entity) => Problems.Conflict(
        $"This {entity} was changed by someone else.",
        "Reload to see the latest changes, then apply your edits again.");
}
