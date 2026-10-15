using FluentValidation;
using PDS.Sales.Domain;

namespace PDS.Sales.Features;

internal sealed class UpdateSaleValidator : AbstractValidator<UpdateSaleRequest>
{
    public UpdateSaleValidator()
    {
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.Status).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Choose a status.")
            .IsInEnum();

        When(x => x.Status is { } s && SaleRules.HasSale(s), () =>
        {
            RuleFor(x => x.BuyerName).Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Enter the buyer's name.")
                .MaximumLength(SaleRules.NameMaxLength);
            RuleFor(x => x.BuyerEmail)
                .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.BuyerEmail))
                .MaximumLength(SaleRules.EmailMaxLength);
            RuleFor(x => x.BuyerPhone).MaximumLength(SaleRules.PhoneMaxLength);
            RuleFor(x => x.BuyerSolicitor).MaximumLength(SaleRules.NameMaxLength);

            RuleFor(x => x.AgreementDate)
                .NotNull().WithMessage("Enter the S&P agreement date.")
                .When(x => SaleRules.RequiresAgreement(x.Status!.Value));

            RuleFor(x => x.SalePriceAmount)
                .NotNull().WithMessage("Enter the sale price.")
                .When(x => SaleRules.RequiresAgreement(x.Status!.Value));
            RuleFor(x => x.SalePriceAmount)
                .GreaterThanOrEqualTo(0m).WithMessage("Sale price cannot be negative.")
                .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");

            RuleFor(x => x.DepositAmount)
                .GreaterThanOrEqualTo(0m).WithMessage("Deposit cannot be negative.")
                .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
            RuleFor(x => x.DepositAmount)
                .Must((x, d) => d is null || x.SalePriceAmount is null || d <= x.SalePriceAmount)
                .WithMessage("Deposit can't be more than the sale price.");

            RuleFor(x => x.UnconditionalDate)
                .NotNull().WithMessage("Enter the unconditional date.")
                .When(x => SaleRules.RequiresUnconditional(x.Status!.Value));
            RuleFor(x => x.UnconditionalDate)
                .Must((x, d) => SaleRules.IsOrdered(x.AgreementDate, d))
                .WithMessage("Unconditional date must be on or after the agreement date.");

            RuleFor(x => x.SettlementDate)
                .NotNull().WithMessage("Enter the settlement date.")
                .When(x => SaleRules.RequiresSettlement(x.Status!.Value));
            RuleFor(x => x.SettlementDate)
                .Must((x, d) => SaleRules.IsOrdered(x.AgreementDate, d) && SaleRules.IsOrdered(x.UnconditionalDate, d))
                .WithMessage("Settlement date must be on or after the agreement and unconditional dates.");
        });
    }
}
