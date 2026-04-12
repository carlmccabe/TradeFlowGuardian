// TradeFlowGuardian.Backtesting/Data/Entities/BacktestEquityPoint.cs

namespace TradeFlowGuardian.Backtesting.Data.Entities;

public class BacktestEquityPoint
{
    public long Id { get; set; }
    public Guid BacktestRunId { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal Balance { get; set; }
    public decimal Equity { get; set; }
    public decimal DrawdownPercent { get; set; }
    
    // Navigation properties
    public BacktestRun BacktestRun { get; set; } = null!;
}