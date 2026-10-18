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
            .Must(a => Math.Abs(a) <= MoneyRules.MaxAbs).WithMessage("Amount is too large.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.")
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
            .Must(a => Math.Abs(a) <= MoneyRules.MaxAbs).WithMessage("Amount is too large.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.")
            .NotEqual(0m).WithMessage("Amount can't be zero.")
            .OverridePropertyName(nameof(IActualCostInput.Amount))
            .When(x => x.Amount is not null);
        RuleFor(x => x.ContractorId).NotNull().WithMessage("Choose a contractor.")
            .When(x => x.Type is ActualCostType.Invoice or ActualCostType.Claim);
        RuleFor(x => x.Status).NotNull().WithMessage("Choose a status.").IsInEnum();
        RuleFor(x => x.PaidDate).NotNull().WithMessage("Enter the paid date.")
            .When(x => x.Status == InvoiceStatus.Paid);
        RuleFor(x => x.DueDate!.Value)
            .GreaterThanOrEqualTo(x => x.Date!.Value).WithMessage("The due date can't be before the invoice date.")
            .OverridePropertyName(nameof(IActualCostInput.DueDate))
            .When(x => x.DueDate is not null && x.Date is not null);
        RuleFor(x => x.GstAmount!.Value).Cascade(CascadeMode.Stop)
            .Must(g => Math.Abs(g) <= MoneyRules.MaxAbs).WithMessage("GST is too large.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.")
            .Must((x, g) => x.Amount is not { } a || Math.Sign(g) != -Math.Sign(a))
            .WithMessage("GST can't be the opposite sign to the amount.")
            .OverridePropertyName(nameof(IActualCostInput.GstAmount))
            .When(x => x.GstAmount is not null);
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
