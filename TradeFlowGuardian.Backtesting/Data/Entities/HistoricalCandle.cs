using System.ComponentModel.DataAnnotations;

namespace TradeFlowGuardian.Backtesting.Data.Entities;

public class HistoricalCandle
{
    public long Id { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string Instrument { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(10)]
    public string Timeframe { get; set; } = string.Empty;
    
    public DateTime Timestamp { get; set; }
    
    [Required]
    public decimal Open { get; set; }
    
    [Required]
    public decimal High { get; set; }
    
    [Required]
    public decimal Low { get; set; }
    
    [Required]
    public decimal Close { get; set; }
    
    public long Volume { get; set; }
    
    public decimal? Spread { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
