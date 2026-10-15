namespace PDS.Sales.Domain;

internal static class SaleRules
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 200;
    public const int PhoneMaxLength = 50;

    /// <summary>Available and Withdrawn carry no sale; every other status does.</summary>
    public static bool HasSale(SaleStatus s) => s is not (SaleStatus.Available or SaleStatus.Withdrawn);

    public static bool RequiresBuyer(SaleStatus s) => HasSale(s);

    public static bool RequiresAgreement(SaleStatus s) =>
        s is SaleStatus.Conditional or SaleStatus.Unconditional or SaleStatus.Settled;

    public static bool RequiresUnconditional(SaleStatus s) => s is SaleStatus.Unconditional or SaleStatus.Settled;

    public static bool RequiresSettlement(SaleStatus s) => s is SaleStatus.Settled;

    /// <summary>Contracted = an S&amp;P agreement is signed (Conditional or later, not Withdrawn).</summary>
    public static bool IsContracted(SaleStatus s) => RequiresAgreement(s);

    public static bool IsOrdered(DateOnly? earlier, DateOnly? later) =>
        earlier is null || later is null || later.Value >= earlier.Value;

    public static SaleDetails Normalise(SaleDetails d) => d with
    {
        BuyerName = UnitRules.Clean(d.BuyerName),
        BuyerEmail = UnitRules.Clean(d.BuyerEmail)?.ToLowerInvariant(),
        BuyerPhone = UnitRules.Clean(d.BuyerPhone),
        BuyerSolicitor = UnitRules.Clean(d.BuyerSolicitor),
    };

    public static void EnsureValid(SaleStatus status, SaleDetails d)
    {
        if (RequiresBuyer(status) && d.BuyerName is null)
            throw new ArgumentException("A buyer is required.", nameof(d));
        if (RequiresAgreement(status) && (d.AgreementDate is null || d.SalePrice is null))
            throw new ArgumentException("Agreement date and sale price are required.", nameof(d));
        if (RequiresUnconditional(status) && d.UnconditionalDate is null)
            throw new ArgumentException("Unconditional date is required.", nameof(d));
        if (RequiresSettlement(status) && d.SettlementDate is null)
            throw new ArgumentException("Settlement date is required.", nameof(d));
        if (d.SalePrice is { Amount: < 0 } || d.Deposit is { Amount: < 0 })
            throw new ArgumentException("Amounts cannot be negative.", nameof(d));
        if (d.SalePrice is { } price && d.Deposit is { } deposit && deposit.Amount > price.Amount)
            throw new ArgumentException("Deposit cannot exceed the sale price.", nameof(d));
        if (!IsOrdered(d.AgreementDate, d.UnconditionalDate) || !IsOrdered(d.AgreementDate, d.SettlementDate)
            || !IsOrdered(d.UnconditionalDate, d.SettlementDate))
            throw new ArgumentException("Sale dates are out of order.", nameof(d));
    }
}
