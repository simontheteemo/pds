using FluentValidation;
using Microsoft.Extensions.Options;
using PDS.Documents.Domain;
using PDS.Documents.Storage;

namespace PDS.Documents.Features;

internal abstract class DocumentMetadataValidator<T> : AbstractValidator<T> where T : IDocumentMetadataInput
{
    protected DocumentMetadataValidator(IOptions<DocumentsOptions> options)
    {
        var categories = options.Value.EffectiveCategories;
        RuleFor(x => x.Title).Cascade(CascadeMode.Stop)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Title is required.")
            .Must(t => t.Trim().Length <= DocumentRules.TitleMaxLength)
            .WithMessage($"Use {DocumentRules.TitleMaxLength} characters or fewer.");
        RuleFor(x => x.Category).Cascade(CascadeMode.Stop)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Choose a category.")
            .Must(c => categories.Contains(c.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Choose one of: {string.Join(", ", categories)}.");
        RuleFor(x => x.Tags)
            .Must(t => DocumentRules.NormaliseTags(t).Count <= DocumentRules.MaxTags)
            .WithMessage($"Use {DocumentRules.MaxTags} tags or fewer.")
            .Must(t => DocumentRules.NormaliseTags(t).All(tag => tag.Length <= DocumentRules.TagMaxLength))
            .WithMessage($"Each tag must be {DocumentRules.TagMaxLength} characters or fewer.");
        RuleFor(x => x.Notes).MaximumLength(DocumentRules.NotesMaxLength);
        When(x => x.Link is not null, () =>
        {
            RuleFor(x => x.Link!.Module).NotNull().WithMessage("Choose the linked record's module.").IsInEnum();
            RuleFor(x => x.Link!.RecordId).NotEmpty().WithMessage("Choose a linked record.");
            RuleFor(x => x.Link!.Label).Cascade(CascadeMode.Stop)
                .Must(l => !string.IsNullOrWhiteSpace(l)).WithMessage("The linked record needs a label.")
                .Must(l => l.Trim().Length <= DocumentRules.LinkLabelMaxLength)
                .WithMessage($"Use {DocumentRules.LinkLabelMaxLength} characters or fewer.");
        });
    }
}

internal sealed class CreateDocumentValidator : DocumentMetadataValidator<CreateDocumentRequest>
{
    public CreateDocumentValidator(IOptions<DocumentsOptions> options) : base(options)
    {
        RuleFor(x => x.UploadId).NotEmpty().WithMessage("Upload the file first.");
        RuleFor(x => x.FileName).Cascade(CascadeMode.Stop)
            .Must(n => FileTypes.CleanFileName(n).Length > 0).WithMessage("Choose a file.")
            .Must(n => FileTypes.CleanFileName(n).Length <= DocumentRules.FileNameMaxLength)
            .WithMessage($"Use a file name of {DocumentRules.FileNameMaxLength} characters or fewer.")
            .Must(n => FileTypes.ContentTypeFor(n) is not null)
            .WithMessage(_ => $"This file type isn't allowed. Allowed: {FileTypes.AllowedList}.");
    }
}

internal sealed class UpdateDocumentValidator : DocumentMetadataValidator<UpdateDocumentRequest>
{
    public UpdateDocumentValidator(IOptions<DocumentsOptions> options) : base(options) =>
        RuleFor(x => x.Version).GreaterThan(0);
}

internal sealed class AddVersionValidator : AbstractValidator<AddVersionRequest>
{
    public AddVersionValidator()
    {
        RuleFor(x => x.UploadId).NotEmpty().WithMessage("Upload the file first.");
        RuleFor(x => x.FileName).Cascade(CascadeMode.Stop)
            .Must(n => FileTypes.CleanFileName(n).Length > 0).WithMessage("Choose a file.")
            .Must(n => FileTypes.CleanFileName(n).Length <= DocumentRules.FileNameMaxLength)
            .WithMessage($"Use a file name of {DocumentRules.FileNameMaxLength} characters or fewer.")
            .Must(n => FileTypes.ContentTypeFor(n) is not null)
            .WithMessage(_ => $"This file type isn't allowed. Allowed: {FileTypes.AllowedList}.");
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

internal sealed class DeleteDocumentValidator : AbstractValidator<DeleteDocumentRequest>
{
    public DeleteDocumentValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
