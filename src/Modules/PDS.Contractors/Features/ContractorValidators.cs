using FluentValidation;
using PDS.Contractors.Domain;

namespace PDS.Contractors.Features;

internal abstract class ContractorInputValidator<T> : AbstractValidator<T> where T : IContractorInput
{
    protected ContractorInputValidator()
    {
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(ContractorRules.NameMaxLength);
        RuleFor(x => x.Type).NotNull().WithMessage("Choose a contractor type.").IsInEnum();
        RuleFor(x => x.Trade).MaximumLength(ContractorRules.TradeMaxLength);
        RuleFor(x => x.Nzbn).Must(v => ContractorRules.NzbnPattern().IsMatch(v!.Trim())).WithMessage("NZBN is 13 digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.Nzbn));
        RuleFor(x => x.ContactName).MaximumLength(ContractorRules.ContactMaxLength);
        RuleFor(x => x.Email).Cascade(CascadeMode.Stop)
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(ContractorRules.EmailMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(ContractorRules.PhoneMaxLength);
        RuleFor(x => x.Address).MaximumLength(ContractorRules.AddressMaxLength);
        RuleFor(x => x.LicenceNumber).MaximumLength(ContractorRules.LicenceMaxLength);
        RuleFor(x => x.Notes).MaximumLength(ContractorRules.NotesMaxLength);
    }
}

internal sealed class CreateContractorValidator : ContractorInputValidator<CreateContractorRequest>;

internal sealed class UpdateContractorValidator : ContractorInputValidator<UpdateContractorRequest>
{
    public UpdateContractorValidator() => RuleFor(x => x.Version).GreaterThan(0);
}

internal sealed class SetActiveRequestValidator : AbstractValidator<SetActiveRequest>
{
    public SetActiveRequestValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
