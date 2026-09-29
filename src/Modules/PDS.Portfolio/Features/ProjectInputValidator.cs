using FluentValidation;
using PDS.Portfolio.Domain;

namespace PDS.Portfolio.Features;

internal abstract class ProjectInputValidator<T> : AbstractValidator<T> where T : IProjectInput
{
    protected ProjectInputValidator()
    {
        RuleFor(x => x.Code).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Code is required.")
            .Must(code => ProjectRules.IsValidCode(ProjectRules.NormaliseCode(code)))
            .WithMessage("Use up to 20 letters, digits or hyphens, starting with a letter or digit.");
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(ProjectRules.NameMaxLength);
        RuleFor(x => x.Stage).NotNull().WithMessage("Choose a stage.").IsInEnum();
        RuleFor(x => x.Status).NotNull().WithMessage("Choose a status.").IsInEnum();
        RuleFor(x => x.Site).NotNull().WithMessage("Site is required.").SetValidator(new SiteValidator());
        RuleFor(x => x.PlannedCompletion)
            .Must((x, end) => ProjectRules.IsOrdered(x.PlannedStart, end))
            .WithMessage("Planned completion must be on or after the planned start.");
        RuleFor(x => x.ActualCompletion)
            .Must((x, end) => ProjectRules.IsOrdered(x.ActualStart, end))
            .WithMessage("Actual completion must be on or after the actual start.");
        RuleFor(x => x.BudgetAmount)
            .GreaterThanOrEqualTo(0m).WithMessage("Budget cannot be negative.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleFor(x => x.ProjectManager).MaximumLength(ProjectRules.ProjectManagerMaxLength);
        RuleFor(x => x.Description).MaximumLength(ProjectRules.DescriptionMaxLength);
    }
}

internal sealed class SiteValidator : AbstractValidator<SiteDto>
{
    public SiteValidator()
    {
        RuleFor(x => x.AddressLine).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(ProjectRules.AddressMaxLength);
        RuleFor(x => x.City).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(ProjectRules.CityMaxLength);
        RuleFor(x => x.Suburb).MaximumLength(ProjectRules.ShortTextMaxLength);
        RuleFor(x => x.Region).MaximumLength(ProjectRules.ShortTextMaxLength);
        RuleFor(x => x.Postcode).MaximumLength(ProjectRules.PostcodeMaxLength);
        RuleFor(x => x.LegalDescription).MaximumLength(ProjectRules.LegalDescriptionMaxLength);
        RuleFor(x => x.TitleReference).MaximumLength(ProjectRules.ShortTextMaxLength);
        RuleFor(x => x.LandAreaSqm).GreaterThanOrEqualTo(0m).WithMessage("Land area cannot be negative.");
    }
}

internal sealed class CreateProjectValidator : ProjectInputValidator<CreateProjectRequest>;

internal sealed class UpdateProjectValidator : ProjectInputValidator<UpdateProjectRequest>
{
    public UpdateProjectValidator() => RuleFor(x => x.Version).GreaterThan(0);
}
