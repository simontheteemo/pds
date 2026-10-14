namespace PDS.Costs.Domain;

internal static class MoneyRules
{
    public const decimal MaxAbs = 999_999_999.99m;

    public static bool IsValidAmount(decimal amount) =>
        decimal.Round(amount, 2) == amount && Math.Abs(amount) <= MaxAbs;
}
