using PDS.Shared.Security;

namespace PDS.Api.People;

public sealed class PeopleOptions
{
    public const string Section = "People";

    /// <summary>When set, people come from this Cognito user pool's role groups; otherwise from Users.</summary>
    public string? UserPoolId { get; set; }

    public List<PersonOptions> Users { get; set; } = [];
}

public sealed class PersonOptions
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Email { get; set; }

    public string Role { get; set; } = Roles.Viewer;
}
