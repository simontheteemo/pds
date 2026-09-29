namespace PDS.Shared.Security;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Viewer = "Viewer";

    public static IReadOnlyList<string> All { get; } = [Admin, Manager, Viewer];
}

public static class Policies
{
    public const string CanRead = "CanRead";
    public const string CanWrite = "CanWrite";
    public const string CanAdminister = "CanAdminister";
}
