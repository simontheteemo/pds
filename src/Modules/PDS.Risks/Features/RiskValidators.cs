using FluentValidation;
using PDS.Risks.Domain;

namespace PDS.Risks.Features;

internal abstract class RiskInputValidator<T> : AbstractValidator<T> where T : IRiskInput
{
    protected RiskInputValidator()
    {
        RuleFor(x => x.Title).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(RiskRules.TitleMaxLength);
        RuleFor(x => x.Category).NotNull().WithMessage("Choose a category.").IsInEnum();
        RuleFor(x => x.Likelihood).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Choose a likelihood.")
            .InclusiveBetween(RiskRules.MinRating, RiskRules.MaxRating).WithMessage("Likelihood must be between 1 and 5.");
        RuleFor(x => x.Impact).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Choose an impact.")
            .InclusiveBetween(RiskRules.MinRating, RiskRules.MaxRating).WithMessage("Impact must be between 1 and 5.");
        RuleFor(x => x.Description).MaximumLength(RiskRules.TextMaxLength);
        RuleFor(x => x.Mitigation).MaximumLength(RiskRules.TextMaxLength);
        RuleFor(x => x.Owner).MaximumLength(RiskRules.OwnerMaxLength);
    }
}

internal sealed class CreateRiskValidator : RiskInputValidator<CreateRiskRequest>;

internal sealed class UpdateRiskValidator : RiskInputValidator<UpdateRiskRequest>
{
    public UpdateRiskValidator()
    {
        RuleFor(x => x.Status).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Choose a status.")
            .IsInEnum()
            .NotEqual(RiskStatus.Closed).WithMessage("Use Close to close a risk.");
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

internal sealed class CloseRiskValidator : AbstractValidator<CloseRiskRequest>
{
    public CloseRiskValidator()
    {
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.Note).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Explain why the risk is being closed.")
            .MaximumLength(RiskRules.ClosingNoteMaxLength);
    }
}

internal sealed class ReopenRiskValidator : AbstractValidator<ReopenRiskRequest>
{
    public ReopenRiskValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
