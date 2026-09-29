using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Shared.Web;

namespace PDS.Portfolio.Features;

internal static class PortfolioProblems
{
    public static ValidationProblem CodeTaken() => Problems.Field("code", "Code is already in use by another project.");

    public static ProblemHttpResult VersionConflict() => Problems.Conflict(
        "This project was changed by someone else.",
        "Reload the project to see the latest changes, then apply your edits again.");
}
