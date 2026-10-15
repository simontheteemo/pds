using FluentValidation;
using PDS.Costs.Domain;

namespace PDS.Costs.Features;

internal abstract class CommitmentInputValidator<T> : AbstractValidator<T> where T : ICommitmentInput
{
    protected CommitmentInputValidator()
    {
        RuleFor(x => x.BudgetLineId).NotNull().WithMessage("Choose a budget line.");
        RuleFor(x => x.Type).NotNull().WithMessage("Choose a type.").IsInEnum();
        RuleFor(x => x.Description).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(CostEntryRules.DescriptionMaxLength);
        RuleFor(x => x.Reference).MaximumLength(CostEntryRules.ReferenceMaxLength);
        RuleFor(x => x.Date).NotNull().WithMessage("Date is required.");
        RuleFor(x => x.Amount).NotNull().WithMessage("Amount is required.");
        RuleFor(x => x.Amount!.Value).Cascade(CascadeMode.Stop)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.")
            .Must(a => Math.Abs(a) <= MoneyRules.MaxAbs).WithMessage("Amount is too large.")
            .OverridePropertyName(nameof(ICommitmentInput.Amount))
            .When(x => x.Amount is not null);
        RuleFor(x => x.Amount!.Value)
            .GreaterThanOrEqualTo(0).WithMessage("Only a variation can be negative.")
            .OverridePropertyName(nameof(ICommitmentInput.Amount))
            .When(x => x.Amount is not null && x.Type != CommitmentType.Variation);
        RuleFor(x => x.Notes).MaximumLength(CostEntryRules.NotesMaxLength);
    }
}

internal abstract class ActualCostInputValidator<T> : AbstractValidator<T> where T : IActualCostInput
{
    protected ActualCostInputValidator()
    {
        RuleFor(x => x.BudgetLineId).NotNull().WithMessage("Choose a budget line.");
        RuleFor(x => x.Type).NotNull().WithMessage("Choose a type.").IsInEnum();
        RuleFor(x => x.Payee).MaximumLength(CostEntryRules.PayeeMaxLength);
        RuleFor(x => x.Description).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(CostEntryRules.DescriptionMaxLength);
        RuleFor(x => x.Reference).MaximumLength(CostEntryRules.ReferenceMaxLength);
        RuleFor(x => x.Date).NotNull().WithMessage("Date is required.");
        RuleFor(x => x.Amount).NotNull().WithMessage("Amount is required.");
        RuleFor(x => x.Amount!.Value).Cascade(CascadeMode.Stop)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.")
            .Must(a => Math.Abs(a) <= MoneyRules.MaxAbs).WithMessage("Amount is too large.")
            .NotEqual(0m).WithMessage("Amount can't be zero.")
            .OverridePropertyName(nameof(IActualCostInput.Amount))
            .When(x => x.Amount is not null);
        RuleFor(x => x.Notes).MaximumLength(CostEntryRules.NotesMaxLength);
    }
}

internal sealed class CreateCommitmentValidator : CommitmentInputValidator<CreateCommitmentRequest>;

internal sealed class UpdateCommitmentValidator : CommitmentInputValidator<UpdateCommitmentRequest>
{
    public UpdateCommitmentValidator() => RuleFor(x => x.Version).GreaterThan(0);
}

internal sealed class CreateActualCostValidator : ActualCostInputValidator<CreateActualCostRequest>;

internal sealed class UpdateActualCostValidator : ActualCostInputValidator<UpdateActualCostRequest>
{
    public UpdateActualCostValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
