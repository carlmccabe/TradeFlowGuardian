
namespace TradeFlowGuardian.Domain.Entities;

public class BotStatus
{
    public bool IsRunning { get; set; }
    public string CurrentStatus { get; set; } = string.Empty;
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    public string Instrument { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public decimal StartOfDayBalance { get; set; }
    public decimal DailyPnL { get; set; }
    public decimal DailyPnLPercent { get; set; }
    public int TradesExecutedToday { get; set; }
    public int MaxTradesPerDay { get; set; }
    public decimal RiskPercent { get; set; }
    public decimal DailyLossLimit { get; set; }
    public string StrategyName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public bool HasOpenPosition { get; set; }
    public string PositionSide { get; set; } = string.Empty;
    public long PositionUnits { get; set; }
    public decimal PositionAvgPrice { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public DateTime NextUpdateIn { get; set; }
    public List<string> RecentActions { get; set; } = new();
    public string LastError { get; set; } = string.Empty;
}
