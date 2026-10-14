using FluentValidation;
using PDS.Costs.Domain;

namespace PDS.Costs.Features;

internal abstract class BudgetLineInputValidator<T> : AbstractValidator<T> where T : IBudgetLineInput
{
    protected BudgetLineInputValidator()
    {
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(BudgetLineRules.NameMaxLength);
        RuleFor(x => x.SortOrder).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Sort order is required.")
            .InclusiveBetween(0, BudgetLineRules.MaxSortOrder).WithMessage("Sort order must be between 0 and 9999.");
        RuleFor(x => x.BudgetAmount).NotNull().WithMessage("Budget is required.");
        RuleFor(x => x.BudgetAmount!.Value).Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(0).WithMessage("Budget can't be negative.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.")
            .LessThanOrEqualTo(MoneyRules.MaxAbs).WithMessage("Budget is too large.")
            .OverridePropertyName(nameof(IBudgetLineInput.BudgetAmount))
            .When(x => x.BudgetAmount is not null);
        RuleFor(x => x.ForecastRemainingAmount!.Value).Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(0).WithMessage("Forecast can't be negative.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.")
            .LessThanOrEqualTo(MoneyRules.MaxAbs).WithMessage("Forecast is too large.")
            .OverridePropertyName(nameof(IBudgetLineInput.ForecastRemainingAmount))
            .When(x => x.ForecastRemainingAmount is not null);
        RuleFor(x => x.Notes).MaximumLength(BudgetLineRules.NotesMaxLength);
    }
}

internal sealed class CreateBudgetLineValidator : BudgetLineInputValidator<CreateBudgetLineRequest>;

internal sealed class UpdateBudgetLineValidator : BudgetLineInputValidator<UpdateBudgetLineRequest>
{
    public UpdateBudgetLineValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
