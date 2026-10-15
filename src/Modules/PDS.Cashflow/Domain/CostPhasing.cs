namespace PDS.Cashflow.Domain;

/// <summary>Straight-line phasing of the remaining cost (spec §3). Replace this to support per-line profiles.</summary>
internal static class CostPhasing
{
    public static IReadOnlyDictionary<DateOnly, decimal> StraightLine(decimal remaining, DateOnly from, DateOnly to)
    {
        var result = new Dictionary<DateOnly, decimal>();
        if (remaining == 0m)
            return result;
        var end = Month.Max(to, from);
        var n = Month.Count(from, end);
        var share = Math.Round(remaining / n, 2, MidpointRounding.AwayFromZero);
        var i = 0;
        foreach (var m in Month.Range(from, end))
            result[m] = ++i == n ? remaining - (share * (n - 1)) : share;
        return result;
    }
}
