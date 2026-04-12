namespace TradeFlowGuardian.Backtesting.Models;

public record BacktestMetrics
{
    public int TotalTrades { get; init; }
    public int WinningTrades { get; init; }
    public int LosingTrades { get; init; }
    public decimal WinRate { get; init; }
    public decimal ProfitFactor { get; init; }
    public decimal AverageWin { get; init; }
    public decimal AverageLoss { get; init; }
    public decimal LargestWin { get; init; }
    public decimal LargestLoss { get; init; }
    public decimal MaxDrawdown { get; init; }
    public decimal SharpeRatio { get; init; }
    public decimal SortinoRatio { get; init; }
    public decimal CalmarRatio { get; init; }
    public TimeSpan AverageTradeDuration { get; init; }
    public decimal ProfitabilityIndex { get; init; }
    public decimal RecoveryFactor { get; init; } // Net Profit / Max Drawdown
    public decimal ExpectancyRatio { get; init; } // (Win% * Avg Win) - (Loss% * Avg Loss)

    /// <summary>P&amp;L and trade stats broken down by calendar month. Ordered chronologically.</summary>
    public List<MonthlyPerformance> MonthlyBreakdown { get; init; } = [];
}

/// <summary>
/// Aggregated performance stats for a single calendar month.
/// Used to detect seasonality and regime drift across the backtest period.
/// </summary>
public record MonthlyPerformance(
    int Year,
    int Month,
    decimal PnL,
    int Trades,
    int Wins,
    decimal WinRate,
    decimal AverageR)
{
    /// <summary>Human-readable label, e.g. "2025-03".</summary>
    public string Label => $"{Year:D4}-{Month:D2}";
}