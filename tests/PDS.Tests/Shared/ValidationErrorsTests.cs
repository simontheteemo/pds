using FluentValidation.Results;
using PDS.Shared.Web;

namespace PDS.Tests.Shared;

public class ValidationErrorsTests
{
    [Theory]
    [InlineData("Code", "code")]
    [InlineData("Site.City", "site.city")]
    [InlineData("PlannedCompletion", "plannedCompletion")]
    [InlineData("", "")]
    public void Converts_property_paths_to_camel_case(string input, string expected) =>
        Assert.Equal(expected, ValidationErrors.ToCamelCasePath(input));

    [Fact]
    public void Groups_messages_by_camel_case_path()
    {
        var result = new ValidationResult([
            new ValidationFailure("Site.City", "City is required."),
            new ValidationFailure("Site.City", "City is required."),
            new ValidationFailure("Name", "Name is required."),
        ]);

        var errors = ValidationErrors.ToDictionary(result);

        Assert.Equal(new[] { "City is required." }, errors["site.city"]);
        Assert.Equal(new[] { "Name is required." }, errors["name"]);
    }
}
