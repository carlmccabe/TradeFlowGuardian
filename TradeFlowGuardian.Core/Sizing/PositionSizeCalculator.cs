namespace TradeFlowGuardian.Core.Sizing;

/// <summary>Which constraint produced the final unit count.</summary>
public enum PositionSizeCap
{
    None,
    MarginLimit,
    MaxUnits
}

/// <param name="RawUnits">Units the pure risk formula asks for (riskAmount / lossPerUnit).</param>
/// <param name="MarginCapUnits">Max units allowed by the margin utilisation limit.</param>
/// <param name="Units">Final size after all caps, rounded to whole units.</param>
/// <param name="BindingCap">Which cap (if any) reduced the size below RawUnits.</param>
/// <param name="LossPerUnit">Account-currency loss per unit if the stop is hit.</param>
public record PositionSizeResult(
    decimal RawUnits,
    decimal MarginCapUnits,
    long Units,
    PositionSizeCap BindingCap,
    decimal LossPerUnit)
{
    public static readonly PositionSizeResult Zero = new(0m, 0m, 0L, PositionSizeCap.None, 0m);

    /// <summary>Risk actually taken (% of balance) at the final size — equals the configured
    /// risk % when no cap binds, lower when one does.</summary>
    public decimal EffectiveRiskPercent(decimal accountBalance) =>
        accountBalance <= 0 ? 0m : Units * LossPerUnit / accountBalance * 100m;
}

/// <summary>
/// The single position-sizing formula shared by the live PositionSizer and the
/// backtest engine, so simulated fills use exactly the sizes the live system would submit.
///
///   raw       = riskAmount / (stopDistance × quoteToAccount)
///   marginCap = (balance × marginUtilisationLimit) / (price × (1/leverage) × quoteToAccount)
///   units     = min(raw, maxPositionUnits, marginCap)
/// </summary>
public static class PositionSizeCalculator
{
    public static PositionSizeResult Calculate(
        decimal accountBalance,
        decimal riskAmount,
        decimal stopDistance,
        decimal quoteToAccount,
        decimal price,
        decimal leverage,
        decimal marginUtilisationLimit,
        decimal maxPositionUnits)
    {
        var lossPerUnit = stopDistance * quoteToAccount;
        if (lossPerUnit <= 0 || riskAmount <= 0 || leverage <= 0)
            return PositionSizeResult.Zero;

        var raw = riskAmount / lossPerUnit;

        var marginRate = 1.0m / leverage;
        var marginCap = price > 0
            ? (accountBalance * marginUtilisationLimit) / (price * marginRate * quoteToAccount)
            : maxPositionUnits;

        var capped = Math.Min(raw, Math.Min(maxPositionUnits, marginCap));

        var bindingCap = capped >= raw
            ? PositionSizeCap.None
            : marginCap < maxPositionUnits ? PositionSizeCap.MarginLimit : PositionSizeCap.MaxUnits;

        return new PositionSizeResult(raw, marginCap, (long)Math.Round(capped), bindingCap, lossPerUnit);
    }
}
