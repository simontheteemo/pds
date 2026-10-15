using FluentValidation;
using PDS.Consents.Domain;

namespace PDS.Consents.Features;

internal abstract class ConditionInputValidator<T> : AbstractValidator<T> where T : IConditionInput
{
    protected ConditionInputValidator()
    {
        RuleFor(x => x.Description).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Describe the condition.")
            .MaximumLength(ConsentRules.ConditionDescriptionMaxLength);
        RuleFor(x => x.Reference).MaximumLength(ConsentRules.ConditionReferenceMaxLength);
    }
}

internal sealed class CreateConditionValidator : ConditionInputValidator<CreateConditionRequest>;

internal sealed class UpdateConditionValidator : ConditionInputValidator<UpdateConditionRequest>
{
    public UpdateConditionValidator() => RuleFor(x => x.Version).GreaterThan(0);
}

internal sealed class CompleteConditionValidator : AbstractValidator<CompleteConditionRequest>
{
    public CompleteConditionValidator()
    {
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.CompletedOn).NotNull().WithMessage("Enter the completion date.");
    }
}

internal sealed class ReopenConditionValidator : AbstractValidator<ReopenConditionRequest>
{
    public ReopenConditionValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
