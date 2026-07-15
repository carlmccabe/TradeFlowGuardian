namespace TradeFlowGuardian.Core.Models;

public record PairedTradeRecord
{
    public string Instrument { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public decimal EntryPrice { get; init; }
    public decimal? ExitPrice { get; init; }
    public long Units { get; init; }
    public DateTimeOffset OpenedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public int? DurationSeconds { get; init; }

    // SL/TP as submitted with the entry order (null for pre-transparency rows / Close signals).
    public decimal? StopLoss { get; init; }
    public decimal? TakeProfit { get; init; }

    // Sizing audit trail captured at order time (null for trades placed before migration 007).
    public decimal? RiskPercent { get; init; }
    public string? RiskSource { get; init; }
    public decimal? AccountBalance { get; init; }
    public decimal? RiskAmount { get; init; }
    public decimal? Atr { get; init; }
    public decimal? StopDistance { get; init; }
    public string? StopSource { get; init; }
    public decimal? QuoteToAud { get; init; }
    public string? CapReason { get; init; }
}
