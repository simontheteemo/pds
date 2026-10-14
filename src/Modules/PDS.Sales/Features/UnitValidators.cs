using FluentValidation;
using PDS.Sales.Domain;

namespace PDS.Sales.Features;

internal abstract class UnitInputValidator<T> : AbstractValidator<T> where T : IUnitInput
{
    protected UnitInputValidator()
    {
        RuleFor(x => x.Identifier).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Identifier is required.")
            .MaximumLength(UnitRules.IdentifierMaxLength);
        RuleFor(x => x.Type).NotNull().WithMessage("Choose a unit type.").IsInEnum();
        RuleFor(x => x.Bedrooms).InclusiveBetween(0, UnitRules.MaxRoomCount);
        RuleFor(x => x.Bathrooms).InclusiveBetween(0, UnitRules.MaxRoomCount);
        RuleFor(x => x.CarParks).InclusiveBetween(0, UnitRules.MaxRoomCount);
        RuleFor(x => x.FloorAreaSqm)
            .InclusiveBetween(0m, UnitRules.MaxAreaSqm)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleFor(x => x.LandAreaSqm)
            .InclusiveBetween(0m, UnitRules.MaxAreaSqm)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleFor(x => x.ListPriceAmount)
            .GreaterThanOrEqualTo(0m).WithMessage("List price cannot be negative.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleFor(x => x.Notes).MaximumLength(UnitRules.NotesMaxLength);
    }
}

internal sealed class CreateUnitValidator : UnitInputValidator<CreateUnitRequest>;

internal sealed class UpdateUnitValidator : UnitInputValidator<UpdateUnitRequest>
{
    public UpdateUnitValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
