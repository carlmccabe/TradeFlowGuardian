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
}