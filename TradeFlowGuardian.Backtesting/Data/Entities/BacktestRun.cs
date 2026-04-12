using System.ComponentModel.DataAnnotations;

namespace TradeFlowGuardian.Backtesting.Data.Entities;

public class BacktestRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string StrategyName { get; set; } = string.Empty;
    
    [Required]
    public string StrategyConfig { get; set; } = string.Empty; // JSON
    
    [Required]
    [MaxLength(20)]
    public string Instrument { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(10)]
    public string Timeframe { get; set; } = string.Empty;
    
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    public decimal InitialBalance { get; set; }
    public decimal FinalBalance { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
    
    public decimal? SharpeRatio { get; set; }
    public decimal? SortinoRatio { get; set; }
    public decimal? CalmarRatio { get; set; }
    public decimal? ProfitFactor { get; set; }
    public decimal? WinRate { get; set; }
    
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    
    public decimal? AverageWin { get; set; }
    public decimal? AverageLoss { get; set; }
    public decimal? LargestWin { get; set; }
    public decimal? LargestLoss { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public List<BacktestTradeEntity> Trades { get; set; } = new();
    public List<BacktestEquityPoint> EquityCurve { get; set; } = new();
}