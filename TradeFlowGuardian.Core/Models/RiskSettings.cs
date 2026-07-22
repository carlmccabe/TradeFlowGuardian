namespace TradeFlowGuardian.Core.Models;

public class RiskSettings
{
    public required string Instrument { get; set; }
    public decimal RiskPercent { get; set; } = 1.5m;

    /// <summary>
    /// Max % of account margin a single trade on this instrument may consume.
    /// Null = use Risk:DefaultMarginCapPercent from config.
    /// </summary>
    public decimal? MarginCapPercent { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
