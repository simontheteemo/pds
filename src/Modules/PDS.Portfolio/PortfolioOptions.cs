namespace PDS.Portfolio;

internal sealed class StandardMilestoneOption
{
    public string? Name { get; set; }

    public string? Category { get; set; }
}

internal sealed class PortfolioOptions
{
    public const string Section = "Portfolio";

    public static readonly IReadOnlyList<StandardMilestoneOption> DefaultStandardMilestones =
    [
        new() { Name = "Land settlement", Category = "Land" },
        new() { Name = "Resource consent lodged", Category = "ResourceConsent" },
        new() { Name = "Resource consent granted", Category = "ResourceConsent" },
        new() { Name = "Building consent lodged", Category = "BuildingConsent" },
        new() { Name = "Building consent granted", Category = "BuildingConsent" },
        new() { Name = "Construction start", Category = "Construction" },
        new() { Name = "Practical completion", Category = "Completion" },
        new() { Name = "Code compliance certificate (CCC)", Category = "Completion" },
        new() { Name = "Titles issued", Category = "Titles" },
        new() { Name = "Settlements complete", Category = "Sales" },
    ];

    /// <summary>Null unless configured. A pre-filled list would be appended to by the config binder.</summary>
    public List<StandardMilestoneOption>? StandardMilestones { get; set; }

    public IReadOnlyList<StandardMilestoneOption> EffectiveStandardMilestones =>
        StandardMilestones is { Count: > 0 } configured ? configured : DefaultStandardMilestones;
}
