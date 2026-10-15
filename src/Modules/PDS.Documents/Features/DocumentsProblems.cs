using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Documents.Storage;
using PDS.Shared.Web;

namespace PDS.Documents.Features;

internal static class DocumentsProblems
{
    public static ValidationProblem ProjectArchived() =>
        Problems.Field("projectId", "Documents can't be uploaded or changed on an archived project.");

    public static ValidationProblem FileTypeNotAllowed() =>
        Problems.Field("fileName", $"This file type isn't allowed. Allowed: {FileTypes.AllowedList}.");

    public static ValidationProblem UploadMissing() =>
        Problems.Field("uploadId", "The upload wasn't found or has expired. Upload the file again.");

    public static ValidationProblem TooLarge(long max) =>
        Problems.Field("sizeBytes", $"The file is larger than the {max / (1024 * 1024)} MB limit.");

    public static ValidationProblem Empty() => Problems.Field("sizeBytes", "The file is empty.");

    public static ValidationProblem TypeMismatch() =>
        Problems.Field("fileName", "The uploaded file doesn't match its file name. Upload it again.");

    public static ProblemHttpResult UploadInProgress() => Problems.Conflict(
        "This upload is already being saved.",
        "Refresh the document list to check whether it was saved, then try again if it's missing.");

    public static ProblemHttpResult VersionConflict() => Problems.Conflict(
        "This document was changed by someone else.",
        "Reload to see the latest changes, then apply your edits again.");
}
