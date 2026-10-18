using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Shared.Web;

namespace PDS.Portfolio.Features;

internal static class MilestoneProblems
{
    public static ValidationProblem ProjectArchived() =>
        Problems.Field("projectId", "Milestones can't be changed on an archived project.");

    public static ValidationProblem DuplicateName() =>
        Problems.Field("name", "A milestone with this name already exists.");

    public static ValidationProblem InvalidVersion() =>
        Problems.Field("version", "Version is required.");

    public static ProblemHttpResult VersionConflict() => Problems.Conflict(
        "This milestone was changed by someone else.",
        "Reload the programme to see the latest changes, then apply your edits again.");
}
