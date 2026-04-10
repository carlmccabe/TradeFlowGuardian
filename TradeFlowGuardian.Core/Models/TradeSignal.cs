using TradeFlowGuardian.Core.Enums;

namespace TradeFlowGuardian.Core.Models;

/// <summary>
/// Inbound signal payload from TradingView webhook alert.
/// Configure TV alert message as JSON matching this structure.
/// </summary>
public class TradeSignal
{
    /// <summary>OANDA instrument format e.g. "USD_JPY", "EUR_USD", "GBP_USD"</summary>
    public required string Instrument { get; init; }

    public required SignalDirection Direction { get; init; }

    /// <summary>ATR value at signal time — used to validate filter server-side</summary>
    public decimal Atr { get; init; }

    /// <summary>Price at signal bar close</summary>
    public decimal Price { get; init; }

    /// <summary>Risk % override — leave 0 to use server config default</summary>
    public decimal RiskPercent { get; init; }

    /// <summary>TV alert timestamp UTC — signals older than 60s are rejected</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Idempotency key — TV alert ID or unique hash to prevent duplicate execution</summary>
    public string? IdempotencyKey { get; init; }
}
