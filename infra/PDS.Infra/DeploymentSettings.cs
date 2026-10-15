using System.Text.Json;
using System.Text.RegularExpressions;

namespace PDS.Infra;

public sealed record BrandingSettings(string ProductName, string? LogoUrl, string PrimaryColor);

public sealed record LocaleSettings(string Currency, string Culture, string TimeZone);

/// <summary>The GitHub repository and environment allowed to assume this account's deploy role.</summary>
public sealed record GitHubSettings(string Repository, string Environment);

/// <summary>Weekday email digest of due consent items. Absent or disabled: nothing is created.</summary>
public sealed record ConsentReminderSettings(bool Enabled, string? Schedule, string? TimeZone, string[]? Emails)
{
    public const string DefaultSchedule = "cron(0 7 ? * MON-FRI *)";
}

/// <summary>One file per deployment in infra/deployments/&lt;name&gt;.json. Client-specific values live only there.</summary>
public sealed partial record DeploymentSettings(
    string Name,
    string Account,
    string Region,
    bool IsProduction,
    string CognitoDomainPrefix,
    string? AlarmEmail,
    decimal MonthlyBudgetUsd,
    BrandingSettings Branding,
    LocaleSettings Locale,
    GitHubSettings? GitHub,
    ConsentReminderSettings? ConsentReminders = null)
{
    public static DeploymentSettings Load(string path)
    {
        var settings = JsonSerializer.Deserialize<DeploymentSettings>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"{path} is empty.");
        if (!NamePattern().IsMatch(settings.Name))
            throw new InvalidOperationException($"Deployment name '{settings.Name}' must be lower-case letters, digits and hyphens.");
        if (settings.ConsentReminders is { Enabled: true } r)
        {
            if ((r.Emails ?? []).Any(e => string.IsNullOrWhiteSpace(e) || !e.Contains('@')))
                throw new InvalidOperationException("consentReminders.emails must contain valid email addresses.");
            if (r.Schedule is { } schedule && !schedule.StartsWith("cron(", StringComparison.Ordinal) && !schedule.StartsWith("rate(", StringComparison.Ordinal))
                throw new InvalidOperationException("consentReminders.schedule must be a cron(...) or rate(...) expression.");
        }
        return settings;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,30}$")]
    private static partial Regex NamePattern();
}

public sealed record AssetPaths(string Api, string Web);
