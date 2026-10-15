namespace PDS.Sales.Features;

/// <summary>
/// Culture-independent natural ordering: digit runs compare numerically, other runs compare
/// ordinally ignoring case. Falls back to ordinal comparison so the order is total and stable.
/// </summary>
internal sealed class NaturalIdentifierComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            var xDigit = char.IsAsciiDigit(x[i]);
            var yDigit = char.IsAsciiDigit(y[j]);
            var xEnd = RunEnd(x, i, xDigit);
            var yEnd = RunEnd(y, j, yDigit);
            var xRun = x.AsSpan(i, xEnd - i);
            var yRun = y.AsSpan(j, yEnd - j);

            int result;
            if (xDigit && yDigit)
            {
                var xt = xRun.TrimStart('0');
                var yt = yRun.TrimStart('0');
                result = xt.Length != yt.Length ? xt.Length.CompareTo(yt.Length) : xt.CompareTo(yt, StringComparison.Ordinal);
                if (result == 0) result = xRun.Length.CompareTo(yRun.Length);
            }
            else if (xDigit != yDigit)
            {
                // Digits sort before non-digits.
                result = xDigit ? -1 : 1;
            }
            else
            {
                result = xRun.CompareTo(yRun, StringComparison.OrdinalIgnoreCase);
            }

            if (result != 0) return Math.Sign(result);
            i = xEnd;
            j = yEnd;
        }

        var remaining = (x.Length - i).CompareTo(y.Length - j);
        return remaining != 0 ? remaining : Math.Sign(string.CompareOrdinal(x, y));
    }

    private static int RunEnd(string s, int start, bool digits)
    {
        var end = start;
        while (end < s.Length && char.IsAsciiDigit(s[end]) == digits) end++;
        return end;
    }
}
