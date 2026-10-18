using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Shared.Web;

namespace PDS.Actions.Features;

internal static class ActionsProblems
{
    public static ValidationProblem ProjectArchived() =>
        Problems.Field("projectId", "Actions can't be added or changed on an archived project.");

    public static ValidationProblem UnknownOwner() =>
        Problems.Field("ownerId", "Choose someone from the people list.");

    public static ValidationProblem ActionClosed() =>
        Problems.Field("status", "Reopen the action before editing it.");

    public static ProblemHttpResult VersionConflict() => Problems.Conflict(
        "This action was changed by someone else.",
        "Reload the actions to see the latest changes, then apply your edits again.");

    public static ProblemHttpResult NotOwner() =>
        TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only the action's owner or a manager can do this.");
}
