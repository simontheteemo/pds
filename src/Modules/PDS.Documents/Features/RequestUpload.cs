using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PDS.Documents.Storage;
using PDS.Portfolio.Contracts;
using PDS.Shared.Security;
using PDS.Shared.Web;

namespace PDS.Documents.Features;

internal sealed record RequestUploadRequest(string FileName, long SizeBytes);

internal sealed record UploadTicket(Guid UploadId, string UploadUrl, string ContentType, DateTimeOffset ExpiresAt);

internal sealed class RequestUploadValidator : AbstractValidator<RequestUploadRequest>
{
    public RequestUploadValidator(IOptions<DocumentsOptions> options)
    {
        RuleFor(x => x.FileName).Cascade(CascadeMode.Stop)
            .Must(n => FileTypes.CleanFileName(n).Length > 0).WithMessage("Choose a file.")
            .Must(n => FileTypes.CleanFileName(n).Length <= Domain.DocumentRules.FileNameMaxLength)
            .WithMessage($"Use a file name of {Domain.DocumentRules.FileNameMaxLength} characters or fewer.")
            .Must(n => FileTypes.ContentTypeFor(n) is not null)
            .WithMessage(_ => $"This file type isn't allowed. Allowed: {FileTypes.AllowedList}.");
        RuleFor(x => x.SizeBytes).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("The file is empty.")
            .LessThanOrEqualTo(options.Value.MaxFileSizeBytes)
            .WithMessage($"The file is larger than the {options.Value.MaxFileSizeBytes / (1024 * 1024)} MB limit.");
    }
}

internal static class RequestUpload
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/uploads", Handle)
            .RequireAuthorization(Policies.CanWrite)
            .WithValidation<RequestUploadRequest>()
            .WithName("RequestDocumentUpload");

    internal static async Task<Results<Ok<UploadTicket>, NotFound, ValidationProblem>> Handle(
        Guid projectId, RequestUploadRequest request, IPortfolioQueries portfolio, IFileStorage storage,
        TimeProvider clock, CancellationToken ct)
    {
        var project = await ProjectLookup.FindAsync(portfolio, projectId, ct);
        if (project is null)
            return TypedResults.NotFound();
        if (project.IsArchived)
            return DocumentsProblems.ProjectArchived();

        var contentType = FileTypes.ContentTypeFor(request.FileName)!;
        var uploadId = Guid.CreateVersion7();
        var expiresAt = clock.GetUtcNow().Add(Lifetime);
        var url = await storage.CreateUploadUrlAsync(
            StorageKeys.Pending(new ProjectId(projectId), uploadId), contentType, expiresAt, ct);
        return TypedResults.Ok(new UploadTicket(uploadId, url, contentType, expiresAt));
    }
}
