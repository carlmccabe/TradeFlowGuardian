namespace TradeFlowGuardian.Core.Models;

public record DailyPnlRecord
{
    /// <summary>ISO date string "YYYY-MM-DD" (daily) or the Monday of the week (weekly).</summary>
    public string Date { get; init; } = string.Empty;

    /// <summary>Realized P&L in quote currency for all closed trades in the bucket.</summary>
    public double Pnl { get; init; }

    public int TradeCount { get; init; }
}
