namespace PDS.Cashflow.Domain;

internal static class Month
{
    public static DateOnly Of(DateOnly d) => new(d.Year, d.Month, 1);
    public static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    public static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    /// <summary>Inclusive count of months from <paramref name="from"/> to <paramref name="to"/> (both first-of-month).</summary>
    public static int Count(DateOnly from, DateOnly to) => ((to.Year - from.Year) * 12) + to.Month - from.Month + 1;
    public static IEnumerable<DateOnly> Range(DateOnly from, DateOnly to)
    {
        for (var m = from; m <= to; m = m.AddMonths(1))
            yield return m;
    }
    public static string Key(DateOnly m) => m.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
}
