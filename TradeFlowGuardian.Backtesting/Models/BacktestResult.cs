// TradeFlowGuardian.Backtesting/Models/BacktestResult.cs

using TradeFlowGuardian.Domain.Entities;

namespace TradeFlowGuardian.Backtesting.Models;

public record BacktestResult
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string StrategyName { get; init; } = string.Empty;
    public string StrategyConfig { get; init; } = string.Empty; // JSON serialized strategy parameters
    public string Instrument { get; init; } = string.Empty;
    public string Timeframe { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal InitialBalance { get; init; }
    public decimal FinalBalance { get; init; }
    public decimal TotalReturn { get; init; }
    public List<BacktestTrade> Trades { get; init; } = new();
    public List<EquityPoint> EquityCurve { get; init; } = new();
    public BacktestMetrics Metrics { get; init; } = new();
    public TimeSpan Duration { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}


