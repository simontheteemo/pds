namespace PDS.Costs;

internal sealed class CostsOptions
{
    public const string Section = "Costs";

    public static readonly IReadOnlyList<string> DefaultStandardLines =
    [
        "Land & acquisition", "Professional fees", "Consents & levies", "Construction",
        "Finance", "Marketing & sales", "Contingency",
    ];

    /// <summary>Null unless configured. A pre-filled list would be appended to by the config binder.</summary>
    public List<string>? StandardLines { get; set; }

    public IReadOnlyList<string> EffectiveStandardLines =>
        StandardLines is { Count: > 0 } configured ? configured : DefaultStandardLines;
}
