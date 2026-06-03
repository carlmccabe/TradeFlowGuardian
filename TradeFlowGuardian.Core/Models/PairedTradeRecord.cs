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
}
