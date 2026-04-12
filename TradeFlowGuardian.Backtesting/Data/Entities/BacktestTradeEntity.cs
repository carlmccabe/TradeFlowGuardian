using System.ComponentModel.DataAnnotations;

namespace TradeFlowGuardian.Backtesting.Data.Entities;

public class BacktestTradeEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BacktestRunId { get; set; }
    public int TradeNumber { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string Instrument { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(10)]
    public string Direction { get; set; } = string.Empty;
    
    public DateTime EntryTime { get; set; }
    public decimal EntryPrice { get; set; }
    public DateTime ExitTime { get; set; }
    public decimal ExitPrice { get; set; }
    
    public decimal Units { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    
    public decimal PnL { get; set; }
    public decimal PnLPercent { get; set; }
    public decimal Commission { get; set; }
    public decimal Slippage { get; set; }
    
    [MaxLength(50)]
    public string ExitReason { get; set; } = string.Empty;
    
    public decimal? MAE { get; set; }
    public decimal? MFE { get; set; }
    
    // Navigation properties
    public BacktestRun BacktestRun { get; set; } = null!;
}