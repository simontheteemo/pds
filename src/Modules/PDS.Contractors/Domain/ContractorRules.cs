using System.Text.RegularExpressions;

namespace PDS.Contractors.Domain;

internal static partial class ContractorRules
{
    public const int NameMaxLength = 200;
    public const int TradeMaxLength = 100;
    public const int ContactMaxLength = 200;
    public const int EmailMaxLength = 200;
    public const int PhoneMaxLength = 50;
    public const int AddressMaxLength = 300;
    public const int LicenceMaxLength = 50;
    public const int NotesMaxLength = 4000;

    [GeneratedRegex("^[0-9]{13}$")]
    public static partial Regex NzbnPattern();

    public static ContractorFields Normalise(ContractorFields f) => f with
    {
        Name = (f.Name ?? string.Empty).Trim(),
        Trade = Clean(f.Trade),
        Nzbn = Clean(f.Nzbn),
        ContactName = Clean(f.ContactName),
        Email = Clean(f.Email)?.ToLowerInvariant(),
        Phone = Clean(f.Phone),
        Address = Clean(f.Address),
        LicenceNumber = Clean(f.LicenceNumber),
        Notes = Clean(f.Notes),
    };

    /// <summary>Guards invariants; the API validators report the same rules as field errors first.</summary>
    public static void EnsureValid(ContractorFields f)
    {
        if (f.Name.Length == 0)
            throw new ArgumentException("Contractor name is required.", nameof(f));
        if (f.Nzbn is not null && !NzbnPattern().IsMatch(f.Nzbn))
            throw new ArgumentException("NZBN is 13 digits.", nameof(f));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
