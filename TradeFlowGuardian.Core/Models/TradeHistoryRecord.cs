namespace TradeFlowGuardian.Core.Models;

/// <summary>
/// Persisted record of every order attempt (entry or close), success or failure.
/// Inserted by the Worker after every PlaceMarketOrderAsync / ClosePositionAsync call.
/// </summary>
public record TradeHistoryRecord
{
    /// <summary>Database-assigned primary key (BIGSERIAL). Zero before insert.</summary>
    public long Id { get; init; }

    public required string Instrument { get; init; }

    /// <summary>"Long", "Short", or "Close"</summary>
    public required string Direction { get; init; }

    /// <summary>Signal bar close price used as the intended entry. Zero for Close signals.</summary>
    public decimal EntryPrice { get; init; }

    /// <summary>Computed stop-loss price. Null for Close signals.</summary>
    public decimal? StopLoss { get; init; }

    /// <summary>Computed take-profit price. Null for Close signals.</summary>
    public decimal? TakeProfit { get; init; }

    /// <summary>Requested units (positive = long, negative = short). Zero for Close signals.</summary>
    public long Units { get; init; }

    /// <summary>Actual fill price returned by OANDA. Null on failure.</summary>
    public decimal? FillPrice { get; init; }

    /// <summary>OANDA order/trade ID. Null on failure.</summary>
    public string? OrderId { get; init; }

    public bool Success { get; init; }

    /// <summary>OANDA error message or internal failure reason. Null on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>UTC timestamp of the order attempt.</summary>
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;

    // ── Sizing audit trail (migration 007) — all null for Close signals ──────

    /// <summary>Risk percent applied at sizing time.</summary>
    public decimal? RiskPercent { get; init; }

    /// <summary>Where the risk percent came from: "signal-override", "db", "config-default".</summary>
    public string? RiskSource { get; init; }

    /// <summary>Account balance (AUD) at sizing time.</summary>
    public decimal? AccountBalance { get; init; }

    /// <summary>AUD at risk if the stop is hit: AccountBalance × RiskPercent/100.</summary>
    public decimal? RiskAmount { get; init; }

    /// <summary>ATR from the signal (0 when the signal supplied explicit SL/TP).</summary>
    public decimal? Atr { get; init; }

    /// <summary>Stop distance in quote-currency price units.</summary>
    public decimal? StopDistance { get; init; }

    /// <summary>Where the stop distance came from: "signal-sl" or "atr×N".</summary>
    public string? StopSource { get; init; }

    /// <summary>Quote-currency → AUD conversion rate used for sizing.</summary>
    public decimal? QuoteToAud { get; init; }

    /// <summary>Which limit reduced the size: null, "margin-cap", "max-position-units", "aborted".</summary>
    public string? CapReason { get; init; }
}
