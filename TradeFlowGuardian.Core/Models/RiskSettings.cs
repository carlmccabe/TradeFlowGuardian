namespace TradeFlowGuardian.Core.Models;

public class RiskSettings
{
    public required string Instrument { get; set; }
    public decimal RiskPercent { get; set; } = 1.5m;
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
