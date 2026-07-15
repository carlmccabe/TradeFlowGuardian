namespace TradeFlowGuardian.Core.Models;

/// <summary>
/// Which calendar window the realized-P&amp;L breakdown covers. Both views return one
/// bucket per UTC day; only the window differs. Bucketed by the trade's <em>close</em>
/// date — i.e. the day the P&amp;L was actually realized.
/// </summary>
public enum PnlRange
{
    /// <summary>The current ISO week (Monday 00:00 UTC → now).</summary>
    Week,

    /// <summary>The current calendar month (1st 00:00 UTC → now).</summary>
    Month,
}

public record DailyPnlRecord
{
    /// <summary>ISO date string "YYYY-MM-DD" — the UTC day the trade was closed.</summary>
    public string Date { get; init; } = string.Empty;

    /// <summary>Realized P&L in quote currency for all closed trades in the bucket.</summary>
    public double Pnl { get; init; }

    public int TradeCount { get; init; }
}
