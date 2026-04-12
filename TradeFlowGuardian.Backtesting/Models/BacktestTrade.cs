namespace TradeFlowGuardian.Backtesting.Models;

public record BacktestTrade
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid BacktestRunId { get; init; }
    public int TradeNumber { get; init; }
    public string Instrument { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty; // "Long" or "Short"
    public DateTime EntryTime { get; init; }
    public decimal EntryPrice { get; init; }
    public DateTime ExitTime { get; init; }
    public decimal ExitPrice { get; init; }
    public decimal Units { get; init; }
    public decimal? StopLoss { get; init; }
    public decimal? TakeProfit { get; init; }
    public decimal PnL { get; init; }
    public decimal PnLPercent { get; init; }
    public decimal Commission { get; init; }
    public decimal Slippage { get; init; }
    public string ExitReason { get; init; } = string.Empty; // "StopLoss", "TakeProfit", "Signal", "TimeStop"
    public decimal? MAE { get; init; } // Maximum Adverse Excursion
    public decimal? MFE { get; init; } // Maximum Favorable Excursion

    public decimal CalculateUnrealizedPnL(decimal currentPrice)
    {
        var priceChange = Direction == "Long" ? currentPrice - EntryPrice : EntryPrice - currentPrice;
        return Units * priceChange;
    }

    public decimal CalculateRealizedPnL(decimal exitPrice)
    {
        var priceChange = Direction == "Long" ? exitPrice - EntryPrice : EntryPrice - exitPrice;
        return Units * priceChange;
    }
}