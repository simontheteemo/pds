namespace PDS.Api.Security;

public enum AuthMode
{
    Gateway,
    Development,
}

public sealed class AuthOptions
{
    public const string Section = "Auth";

    public AuthMode Mode { get; set; } = AuthMode.Gateway;

    /// <summary>Cognito issuer, e.g. https://cognito-idp.ap-southeast-2.amazonaws.com/ap-southeast-2_abc.</summary>
    public string? Authority { get; set; }

    public string? ClientId { get; set; }

    /// <summary>Cognito hosted UI base URL, used by the SPA to sign out.</summary>
    public string? LogoutDomain { get; set; }

    public DevUserOptions DevUser { get; set; } = new();
}

public sealed class DevUserOptions
{
    public string Id { get; set; } = "dev-user";

    public string Name { get; set; } = "Dev User";

    public string? Email { get; set; } = "dev@example.com";

    public string[] Roles { get; set; } = [];
}

internal static class SchemeNames
{
    public const string Selector = "Pds";
    public const string Gateway = "Gateway";
    public const string Development = "Development";
}
