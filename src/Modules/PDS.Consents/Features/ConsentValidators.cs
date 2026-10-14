using FluentValidation;
using PDS.Consents.Domain;

namespace PDS.Consents.Features;

internal abstract class ConsentInputValidator<T> : AbstractValidator<T> where T : IConsentInput
{
    protected ConsentInputValidator()
    {
        RuleFor(x => x.Type).NotNull().WithMessage("Choose a consent type.").IsInEnum();
        RuleFor(x => x.Title).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(ConsentRules.TitleMaxLength);
        RuleFor(x => x.Authority).MaximumLength(ConsentRules.AuthorityMaxLength);
        RuleFor(x => x.Reference).MaximumLength(ConsentRules.ReferenceMaxLength);
        RuleFor(x => x.Status).NotNull().WithMessage("Choose a status.").IsInEnum();

        // Two separate chains on purpose: .When applies to every preceding validator in its chain,
        // and the ordering rule must always run.
        RuleFor(x => x.GrantedDate)
            .NotNull().WithMessage("Enter the date the consent was granted.")
            .When(x => x.Status == ConsentStatus.Granted);
        RuleFor(x => x.GrantedDate)
            .Must((x, granted) => ConsentRules.IsOrdered(x.LodgedDate, granted))
            .WithMessage("The granted date can't be before the lodged date.");
        RuleFor(x => x.ExpiryDate)
            .Must((x, expiry) => ConsentRules.IsOrdered(x.GrantedDate, expiry))
            .WithMessage("The expiry date can't be before the granted date.");
        RuleFor(x => x.Notes).MaximumLength(ConsentRules.NotesMaxLength);
    }
}

internal sealed class CreateConsentValidator : ConsentInputValidator<CreateConsentRequest>;

internal sealed class UpdateConsentValidator : ConsentInputValidator<UpdateConsentRequest>
{
    public UpdateConsentValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
