using FluentValidation;
using PDS.Contractors.Domain;

namespace PDS.Contractors.Features;

internal abstract class ContractInputValidator<T> : AbstractValidator<T> where T : IContractInput
{
    protected ContractInputValidator()
    {
        RuleFor(x => x.ContractorId).NotNull().WithMessage("Choose a contractor.");
        RuleFor(x => x.Title).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(ContractRules.TitleMaxLength);
        RuleFor(x => x.Reference).MaximumLength(ContractRules.ReferenceMaxLength);
        RuleFor(x => x.Form).NotNull().WithMessage("Choose a contract form.").IsInEnum();
        RuleFor(x => x.Status).NotNull().WithMessage("Choose a status.").IsInEnum();
        RuleFor(x => x.ValueAmount!.Value).Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(0).WithMessage("Value can't be negative.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.")
            .OverridePropertyName(nameof(IContractInput.ValueAmount))
            .When(x => x.ValueAmount is not null);
        RuleFor(x => x.RetentionPercent!.Value).Cascade(CascadeMode.Stop)
            .InclusiveBetween(0m, ContractRules.MaxRetentionPercent).WithMessage("Retention must be between 0 and 20.")
            .PrecisionScale(4, 1, ignoreTrailingZeros: true).WithMessage("Use at most 1 decimal place.")
            .OverridePropertyName(nameof(IContractInput.RetentionPercent))
            .When(x => x.RetentionPercent is not null);
        RuleFor(x => x.EndDate)
            .Must((x, end) => ContractRules.IsOrdered(x.StartDate, end))
            .WithMessage("End date can't be before the start date.");
        RuleFor(x => x.Notes).MaximumLength(ContractRules.NotesMaxLength);
    }
}

internal sealed class CreateContractValidator : ContractInputValidator<CreateContractRequest>;

internal sealed class UpdateContractValidator : ContractInputValidator<UpdateContractRequest>
{
    public UpdateContractValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
