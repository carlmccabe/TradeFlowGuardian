namespace TradeFlowGuardian.Core.Models;

/// <summary>
/// Full audit trail of a position-sizing calculation:
/// RiskAmount = AccountBalance × RiskPercent/100
/// LossPerUnit = StopDistance × QuoteToAud
/// RawUnits = RiskAmount / LossPerUnit, then capped by MaxPositionUnits and the margin cap.
/// Persisted with every trade so the dashboard can show exactly how a size was reached.
/// </summary>
public record SizingBreakdown
{
    /// <summary>Final units to submit. Zero means sizing aborted (see CapReason).</summary>
    public long Units { get; init; }

    /// <summary>Risk percent actually applied.</summary>
    public decimal RiskPercent { get; init; }

    /// <summary>Where the risk percent came from: "signal-override", "db", or "config-default".</summary>
    public string RiskSource { get; init; } = string.Empty;

    /// <summary>Account balance (AUD) at sizing time.</summary>
    public decimal AccountBalance { get; init; }

    /// <summary>AUD amount at risk if the stop is hit: AccountBalance × RiskPercent/100.</summary>
    public decimal RiskAmount { get; init; }

    /// <summary>Stop distance in quote-currency price units.</summary>
    public decimal StopDistance { get; init; }

    /// <summary>Where the stop distance came from: "signal-sl" or "atr×N".</summary>
    public string StopSource { get; init; } = string.Empty;

    /// <summary>ATR from the signal (0 when the signal supplied explicit SL/TP).</summary>
    public decimal Atr { get; init; }

    /// <summary>Quote-currency → AUD conversion rate used.</summary>
    public decimal QuoteToAud { get; init; }

    /// <summary>AUD lost per unit if the stop is hit: StopDistance × QuoteToAud.</summary>
    public decimal LossPerUnit { get; init; }

    /// <summary>Uncapped size: RiskAmount / LossPerUnit.</summary>
    public decimal RawUnits { get; init; }

    /// <summary>Units allowed by the per-instrument margin-utilisation cap (default 28%).</summary>
    public decimal MarginCapUnits { get; init; }

    /// <summary>Margin (AUD) already committed by open positions when this trade was sized.</summary>
    public decimal ExistingMarginAud { get; init; }

    /// <summary>Units that fit under the total margin ceiling after existing positions. Null when the check could not run (no price).</summary>
    public decimal? AggregateCapUnits { get; init; }

    /// <summary>Which limit reduced the size: null (none), "margin-cap", "aggregate-margin-cap", "max-position-units", or "aborted".</summary>
    public string? CapReason { get; init; }
}
