using FluentValidation;
using PDS.Portfolio.Domain;

namespace PDS.Portfolio.Features;

internal abstract class MilestoneInputValidator<T> : AbstractValidator<T> where T : IMilestoneInput
{
    protected MilestoneInputValidator()
    {
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(MilestoneRules.NameMaxLength);
        RuleFor(x => x.Category).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Choose a category.")
            .IsInEnum().WithMessage("Choose a category.");
        RuleFor(x => x.Owner).MaximumLength(MilestoneRules.OwnerMaxLength);
        RuleFor(x => x.Notes).MaximumLength(MilestoneRules.NotesMaxLength);
        RuleFor(x => x.SortOrder!.Value)
            .InclusiveBetween(0, MilestoneRules.MaxSortOrder).WithMessage("Sort order must be between 0 and 9999.")
            .OverridePropertyName(nameof(IMilestoneInput.SortOrder))
            .When(x => x.SortOrder is not null);
    }
}

internal sealed class CreateMilestoneValidator : MilestoneInputValidator<CreateMilestoneRequest>;

internal sealed class UpdateMilestoneValidator : MilestoneInputValidator<UpdateMilestoneRequest>
{
    public UpdateMilestoneValidator()
    {
        RuleFor(x => x.SortOrder).NotNull().WithMessage("Sort order is required.");
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
