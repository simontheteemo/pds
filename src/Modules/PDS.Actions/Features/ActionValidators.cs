using FluentValidation;
using PDS.Actions.Domain;

namespace PDS.Actions.Features;

internal abstract class ActionInputValidator<T> : AbstractValidator<T> where T : IActionInput
{
    protected ActionInputValidator()
    {
        RuleFor(x => x.Title).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(ActionRules.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(ActionRules.TextMaxLength);
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Choose an owner.");
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority is not null);
        When(x => x.Link is not null, () =>
        {
            RuleFor(x => x.Link!.Module).NotNull().WithMessage("Choose the linked record's module.").IsInEnum();
            RuleFor(x => x.Link!.RecordId).NotEmpty().WithMessage("Choose a linked record.");
            RuleFor(x => x.Link!.Label).Cascade(CascadeMode.Stop)
                .Must(l => !string.IsNullOrWhiteSpace(l)).WithMessage("The linked record needs a label.")
                .Must(l => l.Trim().Length <= ActionRules.LinkLabelMaxLength)
                .WithMessage($"Use {ActionRules.LinkLabelMaxLength} characters or fewer.");
        });
    }
}

internal sealed class CreateActionValidator : ActionInputValidator<CreateActionRequest>;

internal sealed class UpdateActionValidator : ActionInputValidator<UpdateActionRequest>
{
    public UpdateActionValidator()
    {
        RuleFor(x => x.Status).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Choose a status.")
            .IsInEnum()
            .Must(s => s is ActionStatus.Open or ActionStatus.InProgress)
            .WithMessage("Use Complete or Cancel to close an action.");
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

internal sealed class ActionVersionValidator : AbstractValidator<ActionVersionRequest>
{
    public ActionVersionValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
